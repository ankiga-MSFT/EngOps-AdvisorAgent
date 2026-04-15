using Provider.Interfaces;
using System.Diagnostics;

namespace Provider
{


    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; set; }
        public TrieNode Failure { get; set; }
        public bool IsEndOfWord { get; set; }
        public string Word { get; set; }

        public TrieNode()
        {
            Children = new Dictionary<char, TrieNode>();
            Failure = null!;
            IsEndOfWord = false;
            Word = null!;
        }
    }

    public class TrieProvider : ITrieProvider
    {
        public TrieNode Root { get; private set; }

        public TrieProvider(List<string> productNames)
        {
            Root = new TrieNode();
            BuildTrie(productNames);
            BuildFailureLinks();
            DebugTrie(); // Added for debugging purposes
        }

        private void BuildTrie(List<string> productNames)
        {
            foreach (var productName in productNames)
            {
                var node = Root;

                foreach (var ch in productName)
                {
                    if (!node.Children.ContainsKey(ch))
                    {
                        node.Children[ch] = new TrieNode();
                    }
                    node = node.Children[ch];
                }
                node.IsEndOfWord = true;
                node.Word = productName;
            }
        }

        private void BuildFailureLinks()
        {
            var queue = new Queue<TrieNode>();
            foreach (var child in Root.Children.Values)
            {
                child.Failure = Root;
                queue.Enqueue(child);
            }

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();

                foreach (var kvp in currentNode.Children)
                {
                    char ch = kvp.Key;
                    TrieNode childNode = kvp.Value;
                    queue.Enqueue(childNode);

                    TrieNode failureNode = currentNode.Failure;
                    while (failureNode != null && !failureNode.Children.ContainsKey(ch))
                    {
                        failureNode = failureNode.Failure;
                    }

                    if (failureNode == null)
                    {
                        childNode.Failure = Root;
                    }
                    else
                    {
                        childNode.Failure = failureNode.Children[ch];
                        if (childNode.Failure.IsEndOfWord)
                        {
                            childNode.IsEndOfWord = true;
                        }
                    }
                }
            }
        }

        private void DebugTrie()
        {
            var queue = new Queue<(TrieNode node, string prefix)>();
            queue.Enqueue((Root, ""));

            while (queue.Count > 0)
            {
                var (node, prefix) = queue.Dequeue();

                foreach (var kvp in node.Children)
                {
                    char ch = kvp.Key;
                    TrieNode childNode = kvp.Value;

                    string newPrefix = prefix + ch;
                    Debug.WriteLine($"Node: {newPrefix}, Word: {childNode.Word}, IsEndOfWord: {childNode.IsEndOfWord}");

                    queue.Enqueue((childNode, newPrefix));
                }
            }
        }
    }


}
