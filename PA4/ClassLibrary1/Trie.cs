using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary1
{
    public class Trie
    {
        public Node root;

        public Trie()
        {
            root = new Node('.', 0, null);
        }

        public Node Prefix(string word)
        {
            var current = root;

            for (int i = 0; i < word.Length; i++)
            {
                bool found = false;

                foreach (Node child in current.getChildNodes())
                {
                    if (child.value == word[i])
                    {
                        current = child;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    break;
                }
            }

            return current;
        }

        public void InsertWord(String word)
        {
            var current = Prefix(word);
            for (int i = current.depth; i < word.Length; i++)
            {
                var addNode = new Node(word[i], current.depth + 1, current);
                current.children.Add(addNode);
                current = addNode;
            }
            //This identifier, '!', signals that this is the end of a word
            current.children.Add(new Node('!', current.depth, current));
        }

        public void InsertMany(List<string> words)
        {
            foreach (string word in words)
            {
                InsertWord(word);
            }
        }

        public List<string> SearchAll(string word)
        {
            Node end = Prefix(word);

            System.Diagnostics.Debug.WriteLine(end.value);

            List<string> answers = new List<string>();
            List<string> refinedAnswers = new List<string>();

            DFSSearch(answers, end, word);

            //remove exclamation points
            foreach (string a in answers)
            {
                System.Diagnostics.Debug.WriteLine(a);
                refinedAnswers.Add(a.TrimEnd('!'));
            }

            return refinedAnswers;
        }

        public void DFSSearch(List<string> answers, Node start, string word)
        {
            //word doesn't exist
            if (start.value == '!' && start.depth != word.Length - 1)
            {
                return;
            }

            if (answers.Count >= 10)
            {
                return;
            }

            if (start.value == '!')
            {
                answers.Add(word);
                return;
            }

            //empty word
            if (word.Length < 1)
            {
                return;
            }

            foreach (Node node in start.children)
            {
                DFSSearch(answers, node, word + node.value);
            }
        }
    }
}
