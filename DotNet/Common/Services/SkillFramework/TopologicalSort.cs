using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CXOAI.SkillFramework
{
    public static class TopologicalSort// what order should I run these Tasks
    {
        public static List<string> Sort(Dictionary<string, List<string>> dag, ILogger? logger = null)
        {
            var sanitized = Sanitize(dag);// cleaning up dependencies that point to tasks that don't exist,duplicates,madeup tasks for which skill doesn't exits

            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in sanitized.Keys)
            {
                inDegree.TryAdd(node, 0);//How many tasks must finish before I can start
                adjacency.TryAdd(node, []);//When I finish, who is waiting for me

                foreach (var dep in sanitized[node])
                {
                    inDegree.TryAdd(dep, 0);
                    adjacency.TryAdd(dep, []);

                    adjacency[dep].Add(node);
                    inDegree[node]++;
                }
            }// till here we get something like this  inDegree:  { "0": 0, "1": 0, "2": 0, "3": 3, "4": 1 }[how many task should I finish before I start]
                                                   // adjacency: { "0": ["3"], "1": ["3"], "2": ["3"], "3": ["4"], "4": [] }[ when I finish , who is waiting for me]

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));// pick the task who has no dependency ,i.e who's indegree is 0.
            var result = new List<string>();// final order of execution, we will get after the topological sort, which is done using Queue and inDegree count.

            while (queue.Count > 0)// we will keep processing till we have tasks in the queue which has no dependency, in this case it is  Queue:  ["0", "1", "2"]
            {
                var current = queue.Dequeue();// taking first task from the queue which has no dependency, in this case it is "0"
                result.Add(current);// add it to the result list, which is the final order of execution, in this case it will be result: ["0"]

                foreach (var neighbor in adjacency[current])// look at the tasks which are waiting for the current task to finish, in this case it is ["3"]
                {
                    inDegree[neighbor]--;// reduce the inDegree count for the neighbor task, because one of its dependencies has finished, Who was waiting for "0"? → adjacency["0"] = ["3"]  //→ inDegree["3"] was 3, now 3 - 1 = 2  , still task 3 cant start as 2 dependencies are not finished  
                    if (inDegree[neighbor] == 0)//check if all dependencies for the neighbor task are finished, if inDegree count is 0, it means all dependencies are finished and we can add it to the queue for processing for next iteration.
                        queue.Enqueue(neighbor);// adding to the queue for next iteration as soon as its dependency tasks finsihed.
                }
            }// after processing all the tasks in the queue, we will get the final order of execution in the result list, which is result: ["0", "1", "2", "3", "4"]

            if (result.Count != inDegree.Count)// if it doesn't match, it means there is a cycle in the graph, which means there are tasks that are waiting on each other to finish, and we can't determine a valid execution order. In this case, we will log a warning and return the original key order as a fallback.
            {
                // Cycle detected � fall back to original key order (best effort)
                logger?.LogWarning("Cycle detected in DAG. Falling back to original skill order. DAG: {DAG}", string.Join(", ", sanitized.Select(kv => $"{kv.Key}?[{string.Join(",", kv.Value)}]")));
                return [.. sanitized.Keys];// please note this fallback , will not work, sureshot this is going be either wrong or failure to the user query
            }// to tackle this cyclic dependency issue for now , we have used decompose task planner  with temperature: 0, the probability of the LLM producing a cycle is near zero, but still can happen.

            return result;
        }

        private static Dictionary<string, List<string>> Sanitize(Dictionary<string, List<string>> dag)
        {
            var knownSkills = new HashSet<string>(dag.Keys, StringComparer.OrdinalIgnoreCase);
            var sanitized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in dag)
            {
                // Filter: keep only dependencies that are known skill names, remove self-references
                var validDeps = (entry.Value ?? [])
                    .Where(dep => knownSkills.Contains(dep)
                               && !dep.Equals(entry.Key, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                sanitized.TryAdd(entry.Key, validDeps);
            }

            return sanitized;
        }
    }
}
