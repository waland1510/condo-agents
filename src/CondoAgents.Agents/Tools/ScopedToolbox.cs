using System.ComponentModel;

using CondoAgents.Clients;
using CondoAgents.Domain.Retrieval;
using CondoAgents.Domain.Scoping;

using Microsoft.Extensions.AI;

namespace CondoAgents.Agents.Tools;

/// <summary>Every tool is constructed with the scope already bound. The model chooses queries and document ids; it never sees or passes the boundary.</summary>
public sealed class ScopedToolbox(Scope scope, IRetrievalClient retrieval, IPromptShield shield, IOutboundMessenger? messenger = null)
{
    private readonly Lock _gate = new();
    private readonly List<RetrievedChunk> _evidence = [];
    private readonly string _filter = ScopeFilter.Build(scope);

    public const string SearchDocuments = "search_documents";
    public const string OpenDocument = "open_document";
    public const string CivilCodeLookup = "civil_code_lookup";
    public const string ReserveFundProjection = "reserve_fund_projection";
    public const string SendQuestionToSyndic = "send_question_to_syndic";

    public Scope Scope { get; } = scope;

    /// <summary>Evidence order is stable across the run so labels handed to the model keep meaning the same chunk.</summary>
    public IReadOnlyList<RetrievedChunk> Evidence
    {
        get
        {
            lock (_gate)
            {
                return [.. _evidence];
            }
        }
    }

    public IReadOnlyList<AITool> All() =>
    [
        AIFunctionFactory.Create(SearchDocumentsAsync, SearchDocuments, "Search the building's own documents (bylaws, minutes, financial statements, reserve-fund study) and the Civil Code reference. Returns labelled passages to cite as evidence."),
        AIFunctionFactory.Create(OpenDocumentAsync, OpenDocument, "Read every passage of one document by its id, in page order."),
        AIFunctionFactory.Create(CivilCodeLookupAsync, CivilCodeLookup, "Search only the Civil Code of Québec reference corpus on divided co-ownership."),
        AIFunctionFactory.Create(ReserveFundProjectionTool.Project, ReserveFundProjection, "Deterministic projection of a contingency fund balance; use it instead of doing arithmetic."),
    ];

    /// <summary>Wrapped as approval-required: the framework returns the call to the orchestrator instead of invoking it, and only an explicit human response lets it run.</summary>
    public IReadOnlyList<AITool> Irreversible() =>
    [
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendQuestionToSyndicAsync, SendQuestionToSyndic, "Send a written question to the building's syndicate (board). This leaves the system and cannot be undone; use it only when the documents cannot answer and the user asked to contact the syndicate.")),
    ];

    public IReadOnlyList<AITool> ReferenceOnly() =>
    [
        AIFunctionFactory.Create(CivilCodeLookupAsync, CivilCodeLookup, "Search only the Civil Code of Québec reference corpus on divided co-ownership."),
    ];

    public IReadOnlyList<AITool> FinanceOnly() =>
    [
        AIFunctionFactory.Create(SearchDocumentsAsync, SearchDocuments, "Search the building's financial statements, minutes and reserve-fund study for amounts."),
        AIFunctionFactory.Create(ReserveFundProjectionTool.Project, ReserveFundProjection, "Deterministic projection of a contingency fund balance; use it instead of doing arithmetic."),
    ];

    private async Task<IReadOnlyList<EvidencePassage>> SearchDocumentsAsync(
        [Description("What to look for, in the document's language or English.")] string query,
        CancellationToken ct = default)
        => await RetrieveAsync(query, _filter, ct);

    private async Task<IReadOnlyList<EvidencePassage>> OpenDocumentAsync(
        [Description("Document id as shown in a search result, for example bylaws-2024.")] string documentId,
        CancellationToken ct = default)
        => await RetrieveAsync(string.Empty, ScopeFilter.ForDocument(Scope, documentId), ct, top: 64);

    private async Task<IReadOnlyList<EvidencePassage>> CivilCodeLookupAsync(
        [Description("Topic or article number, for example 'contingency fund' or '1071'.")] string topic,
        CancellationToken ct = default)
        => await RetrieveAsync(topic, ScopeFilter.ReferenceOnly(), ct);

    private async Task<string> SendQuestionToSyndicAsync(
        [Description("The question to send, written for the board.")] string message,
        CancellationToken ct = default)
    {
        if (messenger is null)
        {
            throw new InvalidOperationException("No outbound messenger is configured for this scope.");
        }

        return await messenger.SendAsync($"syndicate:{Scope.BuildingId}", message, ct);
    }

    private async Task<IReadOnlyList<EvidencePassage>> RetrieveAsync(string query, string filter, CancellationToken ct, int top = 8)
    {
        using var activity = Telemetry.Source.StartActivity("agent.retrieve");
        var chunks = await retrieval.RetrieveAsync(query, filter, top, ct);

        var verdict = await shield.AnalyzeAsync(query, chunks.Select(c => c.Text).ToList(), ct);
        var attacked = verdict.AttackedDocumentIndexes.ToHashSet();
        var clean = chunks.Where((_, i) => !attacked.Contains(i)).ToList();
        activity?.SetTag("retrieval.chunks", chunks.Count);
        activity?.SetTag("retrieval.withheld", attacked.Count);

        lock (_gate)
        {
            var passages = new List<EvidencePassage>();
            foreach (var chunk in clean)
            {
                var position = _evidence.FindIndex(e => e.ChunkId == chunk.ChunkId);
                if (position < 0)
                {
                    _evidence.Add(chunk);
                    position = _evidence.Count - 1;
                }

                passages.Add(new EvidencePassage(RetrievedChunk.EvidenceLabel(position), chunk.DocumentId, chunk.Title, chunk.Page, chunk.Text));
            }

            return passages;
        }
    }
}

public sealed record EvidencePassage(string Label, string DocumentId, string Title, int? Page, string Text);