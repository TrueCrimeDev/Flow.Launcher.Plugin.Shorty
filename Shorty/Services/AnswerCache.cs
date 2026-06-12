using System.Collections.Concurrent;
using Shorty.Models;

namespace Shorty.Services;

public sealed class AnswerCache
{
    private readonly ConcurrentDictionary<string, AnswerEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AnswerRequest> _expiredRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();
    private readonly object _gate = new();
    private int _capacity;

    public AnswerCache(int capacity = 50)
    {
        _capacity = Math.Clamp(capacity, 1, 200);
    }

    public int Capacity
    {
        get
        {
            lock (_gate)
            {
                return _capacity;
            }
        }
    }

    public void SetCapacity(int capacity)
    {
        lock (_gate)
        {
            _capacity = Math.Clamp(capacity, 1, 200);
            EvictOverCapacity();
        }
    }

    public void Set(AnswerEntry entry)
    {
        _entries[entry.Id] = entry;
        _expiredRequests[entry.Id] = new AnswerRequest(entry.Id, entry.Preset, entry.Question);

        lock (_gate)
        {
            Touch(entry.Id);
            EvictOverCapacity();
        }
    }

    public bool TryGet(string id, out AnswerEntry entry)
    {
        if (_entries.TryGetValue(id, out var found))
        {
            lock (_gate)
            {
                Touch(id);
            }

            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public bool TryGetExpiredRequest(string id, out AnswerRequest request)
    {
        return _expiredRequests.TryGetValue(id, out request!);
    }

    private void Touch(string id)
    {
        if (_nodes.TryGetValue(id, out var existing))
        {
            _lru.Remove(existing);
        }

        var node = _lru.AddLast(id);
        _nodes[id] = node;
    }

    private void EvictOverCapacity()
    {
        while (_entries.Count > _capacity && _lru.First is not null)
        {
            var id = _lru.First.Value;
            _lru.RemoveFirst();
            _nodes.Remove(id);
            _entries.TryRemove(id, out _);
        }
    }
}
