namespace ImeIntegration.Core;

internal sealed class ImeCompositionState
{
    public bool Active { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int SelectionStart { get; private set; }
    public int SelectionLength { get; private set; }
    public IReadOnlyList<string> Candidates => _candidates;
    public int CandidateIndex { get; private set; } = -1;

    private readonly List<string> _candidates = [];

    public void Update(
        bool active,
        string? text,
        int selectionStart,
        int selectionLength,
        IReadOnlyList<string>? candidates,
        int candidateIndex
    )
    {
        Active = active;
        Text = active ? text ?? string.Empty : string.Empty;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
        CandidateIndex = active ? candidateIndex : -1;

        _candidates.Clear();
        if (!active || candidates is null || candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            _candidates.Add(candidate ?? string.Empty);
        }
    }

    public void Clear() => Update(false, null, 0, 0, null, -1);
}
