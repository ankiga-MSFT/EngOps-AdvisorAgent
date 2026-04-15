using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Provider.Interfaces;


namespace Provider
{


    public class JsonExpressionProvider : IJsonExpressionProvider
    {
        private readonly Dictionary<string, string> _aliases;
        private readonly List<string> _ruleStrings;
        private readonly bool caseInsensitive;
        private readonly string rulesJson;
        private Func<JObject, bool> _compiledExpression;
        public JsonExpressionProvider(string rulesJson, bool caseInsensitive = false)
        {
            try
            {
                this.caseInsensitive = caseInsensitive;
                var rulesData = JsonConvert.DeserializeObject<RulesData>(rulesJson) ?? new RulesData();
                _aliases = rulesData.Aliases ?? new Dictionary<string, string>();
                _ruleStrings = NormalizeRules(rulesData.Rules ?? new List<string>());
                _compiledExpression = CompileExpression();
            }
            catch (JsonReaderException ex)
            {
                throw new ArgumentException("Invalid JSON format", ex);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Error compiling expression", ex);
            }

            this.rulesJson = rulesJson;
        }

        public string Rules { get { return rulesJson; } }
        private List<string> NormalizeRules(List<string> rules)
        {
            var normalizedRules = new List<string>();
            foreach (var rule in rules)
            {
                // Remove leading/trailing spaces and concatenate lines
                normalizedRules.Add(rule.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim());
            }
            return normalizedRules;
        }

        private Func<JObject, bool> CompileExpression()
        {
            var parameter = Expression.Parameter(typeof(JObject), "eventObj");
            var rulesExpression = BuildRuleExpression(parameter, _ruleStrings);
            var lambda = Expression.Lambda<Func<JObject, bool>>(rulesExpression, parameter);
            lamdaExpression = lambda.ToString();
            return _compiledExpression = lambda.Compile();
        }

        private string lamdaExpression = string.Empty;

        public JsonExpressionResult Result
        {
            get
            {
                return new JsonExpressionResult
                {
                    Rules = rulesJson,
                    LamdaExpression = LamdaExpression,
                    ValidationDelegate = _compiledExpression
                };
            }
        }        

        public string LamdaExpression { get { return lamdaExpression; } }


        private Expression BuildRuleExpression(ParameterExpression parameter, List<string> ruleStrings)
        {
            Expression expression = null!;

            foreach (var ruleString in ruleStrings)
            {
                var ruleExpression = BuildSingleRuleExpression(parameter, ruleString);

                if (expression == null)
                {
                    expression = ruleExpression;
                }
                else
                {
                    expression = Expression.AndAlso(expression, ruleExpression);
                }
            }

            return expression ?? Expression.Constant(true);
        }

        private List<string> Tokenize(string ruleString)
        {
            var tokens = new List<string>();
            var currentToken = new StringBuilder();
            bool insideQuote = false;
            char quoteChar = '\0';

            foreach (char c in ruleString)
            {
                if (insideQuote)
                {
                    if (c == quoteChar)
                    {
                        insideQuote = false;
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                }
                else
                {
                    if (c == '"' || c == '\'')
                    {
                        insideQuote = true;
                        quoteChar = c;
                        if (currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                    }
                    else if (c == '(' || c == ')')
                    {
                        if (currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                        tokens.Add(c.ToString());
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens;
        }

        private Expression BuildSingleRuleExpression(ParameterExpression parameter, string ruleString)
        {
            var tokens = Tokenize(ruleString);
            return ParseTokens(parameter, tokens);
        }

        private Expression ParseTokens(ParameterExpression parameter, List<string> tokens)
        {
            if (tokens.Count == 0)
            {
                return Expression.Constant(true);
            }

            var stack = new Stack<Expression>();
            int i = 0;

            while (i < tokens.Count)
            {
                var token = tokens[i];

                if (token == "and" || token == "or")
                {
                    if (stack.Count < 1)
                    {
                        throw new InvalidOperationException("Invalid expression format.");
                    }

                    var left = stack.Pop();
                    i++;  // Move to next token which should be the condition after "and" or "or"

                    var right = ParseSubExpression(parameter, tokens.Skip(i).ToList(), out int skipCount);
                    stack.Push(token == "and" ? Expression.AndAlso(left, right) : Expression.OrElse(left, right));
                    i += skipCount;
                }
                else if (token == "(")
                {
                    var subExpression = ParseSubExpression(parameter, tokens.Skip(i + 1).ToList(), out int skipCount);
                    stack.Push(subExpression);
                    i += skipCount + 1;  // Move past the sub-expression and closing parenthesis
                }
                else if (i + 2 < tokens.Count && (tokens[i + 1] == "isNullOrEmpty" || tokens[i + 1] == "isNotNullOrEmpty"))
                {
                    // Handle special conditions
                    var condition = token + " " + tokens[i + 1] + " " + tokens[i + 2];
                    var ruleExpression = CreateExpressionFromCondition(parameter, condition);
                    stack.Push(ruleExpression);
                    i += 3;  // Move past the special condition
                }
                else if (i + 3 < tokens.Count && tokens[i + 1] == "in" && tokens[i + 2].StartsWith("["))
                {
                    // Handle 'in' condition
                    var field = token;
                    i += 2;  // Move to the opening bracket [

                    var listTokens = new List<string>();
                    while (i < tokens.Count && tokens[i] != "]")
                    {
                        listTokens.Add(tokens[i]);
                        i++;
                    }

                    if (i >= tokens.Count || tokens[i] != "]")
                    {
                        throw new ArgumentException("Invalid rule format: missing closing bracket for 'in' condition.");
                    }

                    listTokens.Add("]");  // Include the closing bracket
                    var condition = field + " in " + string.Join(" ", listTokens);
                    var ruleExpression = CreateExpressionFromCondition(parameter, condition);
                    stack.Push(ruleExpression);
                    i++;  // Move past the closing bracket
                }
                else
                {
                    if (i + 2 < tokens.Count)
                    {
                        var condition = token + " " + tokens[i + 1] + " " + tokens[i + 2];
                        var ruleExpression = CreateExpressionFromCondition(parameter, condition);
                        stack.Push(ruleExpression);
                        i += 3;
                    }
                    else
                    {
                        throw new ArgumentException("Invalid rule format");
                    }
                }
            }

            return stack.Count > 0 ? stack.Pop() : Expression.Constant(true);
        }


        private Expression ParseSubExpression(ParameterExpression parameter, List<string> tokens, out int skipCount)
        {
            var subExpressionTokens = new List<string>();
            int subExpressionLevel = 0;
            int i = 0;

            while (i < tokens.Count)
            {
                var subToken = tokens[i];
                if (subToken == "(")
                {
                    subExpressionLevel++;
                }
                if (subToken == ")")
                {
                    subExpressionLevel--;
                }

                subExpressionTokens.Add(subToken);
                i++;

                if (subExpressionLevel < 0)
                {
                    subExpressionTokens.RemoveAt(subExpressionTokens.Count - 1);
                    break;
                }
            }

            skipCount = i;
            return ParseTokens(parameter, subExpressionTokens);
        }





        private Expression CreateExpressionFromCondition(ParameterExpression parameter, string condition)
        {
            var tokens = condition.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                throw new ArgumentException("Invalid rule format");
            }

            var fieldName = tokens[0];
            var fieldAlias = _aliases.ContainsKey(fieldName) ? _aliases[fieldName] : fieldName;
            Expression field = Expression.Call(typeof(JsonExtensions), nameof(JsonExtensions.SelectTokens), null, parameter, Expression.Constant(fieldAlias));


            var param = Expression.Parameter(typeof(JToken), "fieldToken");
            var normalizedOperator = tokens[1].ToLower();
            Expression TransformToLower(Expression expr)
            {
                return Expression.Call(expr, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            }

            Expression  leftOperand = Expression.Convert(param, typeof(string));
            Expression rightOperand = ParseValue(tokens[2]);

            if (caseInsensitive && (normalizedOperator == "==" || normalizedOperator == "!=" ||
                                    normalizedOperator == "contains" || normalizedOperator == "beginswith" ||
                                    normalizedOperator == "endswith"))
            {
                leftOperand = TransformToLower(leftOperand);
                rightOperand = TransformToLower(rightOperand);
            }



            var expression = normalizedOperator switch
            {
                "exists" => Expression.Lambda(Expression.Call(typeof(Enumerable), "Any", new[] { typeof(JToken) }, field, Expression.Lambda(Expression.Constant(true), param)), param),
                "notexists" => Expression.Lambda(Expression.Call(typeof(Enumerable), "Any", new[] { typeof(JToken) }, field, Expression.Lambda(Expression.Constant(true), param)), param),
                "==" => Expression.Lambda(Expression.Equal(leftOperand, rightOperand), param),
                "!=" => Expression.Lambda(Expression.NotEqual(leftOperand, rightOperand), param),
                ">" => Expression.Lambda(Expression.GreaterThan(Expression.Convert(param, typeof(int)), ParseValue(tokens[2])), param),
                "<" => Expression.Lambda(Expression.LessThan(Expression.Convert(param, typeof(int)), ParseValue(tokens[2])), param),
                ">=" => Expression.Lambda(Expression.GreaterThanOrEqual(Expression.Convert(param, typeof(int)), ParseValue(tokens[2])), param),
                "<=" => Expression.Lambda(Expression.LessThanOrEqual(Expression.Convert(param, typeof(int)), ParseValue(tokens[2])), param),
                "contains" => Expression.Lambda(Expression.Call(leftOperand, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, rightOperand), param),
                "beginswith" => Expression.Lambda(Expression.Call(leftOperand, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, rightOperand), param),
                "endswith" => Expression.Lambda(Expression.Call(leftOperand, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, rightOperand), param),
                "is" => tokens[2].ToLower() switch
                {
                    "null" => Expression.Lambda(Expression.Equal(Expression.Property(param, "Type"),Expression.Constant(JTokenType.Null)),param),              
                    "notnull" => Expression.Lambda(Expression.NotEqual(Expression.Property(param, "Type"),Expression.Constant(JTokenType.Null)),param),             
                    "true" => Expression.Lambda(Expression.Equal(Expression.Convert(param, typeof(bool)), Expression.Constant(true)), param),
                    "false" => Expression.Lambda(Expression.Equal(Expression.Convert(param, typeof(bool)), Expression.Constant(false)), param),
                    _ => throw new NotSupportedException($"Condition {tokens[2]} is not supported.")
                },
                "isnullorempty" => Expression.Lambda(Expression.Call(typeof(string).GetMethod("IsNullOrEmpty", new[] { typeof(string) })!, Expression.Convert(param, typeof(string))), param),
                "isnotnullorempty" => Expression.Lambda(Expression.Not(Expression.Call(typeof(string).GetMethod("IsNullOrEmpty", new[] { typeof(string) })!, Expression.Convert(param, typeof(string)))), param),
                "isnullorwhitespace" => Expression.Lambda(Expression.Call(typeof(string).GetMethod("IsNullOrWhiteSpace", new[] { typeof(string) })!, Expression.Convert(param, typeof(string))), param),
                "isnotnullorwhitespace" => Expression.Lambda(Expression.Not(Expression.Call(typeof(string).GetMethod("IsNullOrWhiteSpace", new[] { typeof(string) })!, Expression.Convert(param, typeof(string)))), param),
                "regexmatch" => Expression.Lambda(Expression.Call(typeof(Regex).GetMethod("IsMatch", new[] { typeof(string), typeof(string) })!, Expression.Convert(param, typeof(string)), Expression.Constant(tokens[2].Trim('\''))), param),
                "length" => Expression.Lambda(Expression.Equal(Expression.PropertyOrField(Expression.Convert(param, typeof(string)), "Length"), Expression.Constant(int.Parse(tokens[2]))), param),
                "in" => CreateInExpression(tokens, param),
                _ => throw new NotSupportedException($"Condition {tokens[1]} is not supported.")
            };
            if (normalizedOperator== "notexists")
                return Expression.Not(Expression.Call(typeof(Enumerable), "Any", new[] { typeof(JToken) }, field, expression));
            else
                return Expression.Call(typeof(Enumerable), "Any", new[] { typeof(JToken) }, field, expression);

        }



        private Expression CreateInExpression(string[] tokens, ParameterExpression param)
        {
            var values = string.Join(" ", tokens.Skip(2))
                .Trim('[', ']').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().Trim('\'')).ToArray();

            var methodInfo = typeof(InExpressionHelper).GetMethod(nameof(InExpressionHelper.InHelper), BindingFlags.Static | BindingFlags.Public);
            var arrayExpression = Expression.Constant(values);
            var caseInsensitiveExpression = Expression.Constant(caseInsensitive);
            var callExpression = Expression.Call(methodInfo!, arrayExpression, Expression.Convert(param, typeof(string)), caseInsensitiveExpression);
            return Expression.Lambda(callExpression, param);
        }


        private Expression ParseValue(string value)
        {
            if (bool.TryParse(value, out bool boolValue))
            {
                return Expression.Constant(boolValue);
            }
            if (int.TryParse(value, out int intValue))
            {
                return Expression.Constant(intValue);
            }
            return Expression.Constant(value.Trim('\''));
        }
        //private Expression CreateNotExistsExpression(string[] tokens, ParameterExpression param)
        //{
        //    var fieldName = tokens[0];
        //    var fieldAlias = _aliases.ContainsKey(fieldName) ? _aliases[fieldName] : fieldName;

        //    //var methodInfo = typeof(NotExistsHelper).GetMethod("NotExistsHelperMethod", BindingFlags.Static |BindingFlags. | BindingFlags.Public);
        //    var fieldAliasExpression = Expression.Parameter( typeof(string),fieldAlias);
        //    var methodExpression = Expression.Call(typeof(NotExistsHelper).GetMethod("NotExistsHelperMethod")!,param, fieldAliasExpression);
        //    var lambda = Expression.Lambda<Func<JToken, string, bool>>(methodExpression, param, fieldAliasExpression);
        //    return lambda;
        //}
    }
    
#pragma warning disable CS8618 

    //public static class NotExistsHelper
    //{
    //    public static bool NotExistsHelperMethod(JToken param, string fieldAlias)
    //    {
    //        var result = param.SelectTokens(fieldAlias).Any();
    //        return !result;
    //    }
    //}

    public class InExpressionHelper
    {
        public static bool InHelper(string[] values, string value, bool caseInsensitive)
        {
            if (caseInsensitive)
            {
                values = values.Select(v => v.ToLower()).ToArray();
                value = value.ToLower();
            }
            return values.Contains(value);
        }
    }

    public class RulesData
    {
        public Dictionary<string, string> Aliases { get; set; }
        public List<string> Rules { get; set; }
    }

    public static class JsonExtensions
    {
        public static IEnumerable<JToken> SelectTokens(this JObject jObject, string path)
        {
            return jObject.SelectTokens(path);
        }
    }

    public class JsonExpressionResult
    {
        public string Rules { get; set; }
        public string LamdaExpression { get; set; }

        public Func<JObject, bool> ValidationDelegate { get; set; }
    }


}
