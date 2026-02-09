using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Infrastructure.Caching;

/// <summary>
/// Least Recently Used (LRU) eviction policy
/// Implements O(1) operations using Dictionary + Manual Doubly Linked List
/// Uses ONLY primitive collections as per requirements (Dictionary, no .NET LinkedList)
/// </summary>
public class LruEvictionPolicy : IEvictionPolicy
{
    /// <summary>
    /// Manual doubly-linked list node
    /// Implements linked list functionality using only primitive types and references
    /// </summary>
    private class Node
    {
        public string Key { get; set; }
        public CachedData Value { get; set; }
        public Node? Previous { get; set; }
        public Node? Next { get; set; }

        public Node(string key, CachedData value)
        {
            Key = key;
            Value = value;
        }
    }

    private readonly int _capacity;
    private readonly Dictionary<string, Node> _cache; // O(1) lookup
    private Node? _head; // Most recently used
    private Node? _tail; // Least recently used
    private readonly object _lock = new();

    public int Capacity => _capacity;
    public int Count { get; private set; }

    public LruEvictionPolicy(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentException("Capacity must be at least 1", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<string, Node>(capacity);
        Count = 0;
    }

    /// <summary>
    /// Tries to get a value from cache and moves it to the front (most recently used)
    /// Time Complexity: O(1)
    /// </summary>
    public bool TryGet(string key, out CachedData? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to head (most recently used)
                MoveToHead(node);

                // Update last accessed time
                value = node.Value;
                value.LastAccessedAt = DateTime.UtcNow;
                return true;
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// Sets a value in cache, evicting the least recently used item if at capacity
    /// Time Complexity: O(1)
    /// </summary>
    public void Set(string key, CachedData value)
    {
        lock (_lock)
        {
            // Update existing entry
            if (_cache.TryGetValue(key, out var existingNode))
            {
                existingNode.Value = value;
                MoveToHead(existingNode);
                return;
            }

            // Evict least recently used if at capacity
            if (Count >= _capacity)
            {
                RemoveTail();
            }

            // Add new entry at head (most recently used)
            var newNode = new Node(key, value);
            AddToHead(newNode);
            _cache[key] = newNode;
            Count++;
        }
    }

    /// <summary>
    /// Removes a specific item from the cache (cache invalidation)
    /// Time Complexity: O(1)
    /// </summary>
    public bool Remove(string key)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var node))
                return false;

            RemoveNode(node);
            _cache.Remove(key);
            Count--;
            return true;
        }
    }

    /// <summary>
    /// Clears all items from the cache
    /// Time Complexity: O(1)
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _head = null;
            _tail = null;
            Count = 0;
        }
    }

    #region Manual Doubly Linked List Operations

    /// <summary>
    /// Adds a node to the head of the list (most recently used position)
    /// </summary>
    private void AddToHead(Node node)
    {
        node.Next = _head;
        node.Previous = null;

        if (_head != null)
            _head.Previous = node;

        _head = node;

        if (_tail == null)
            _tail = node;
    }

    /// <summary>
    /// Removes a node from the list
    /// </summary>
    private void RemoveNode(Node node)
    {
        if (node.Previous != null)
            node.Previous.Next = node.Next;
        else
            _head = node.Next; // Node was head

        if (node.Next != null)
            node.Next.Previous = node.Previous;
        else
            _tail = node.Previous; // Node was tail
    }

    /// <summary>
    /// Moves an existing node to the head (marks as most recently used)
    /// </summary>
    private void MoveToHead(Node node)
    {
        // Already at head, no need to move
        if (node == _head)
            return;

        RemoveNode(node);
        AddToHead(node);
    }

    /// <summary>
    /// Removes the tail node (least recently used item)
    /// </summary>
    private void RemoveTail()
    {
        if (_tail == null)
            return;

        var tailKey = _tail.Key;
        RemoveNode(_tail);
        _cache.Remove(tailKey);
        Count--;
    }

    #endregion
}
