using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AffixGenerator.Generator
{
    public class WordAnalysis
    {
        public string Root { get; set; } = "";
        public List<SuffixAnalysis> Suffixes { get; set; } = new();
        public string OriginalWord { get; set; } = "";
        public string MatchedRule { get; set; } = "";
        public string RuleDescription { get; set; } = "";
        public string RuleId { get; set; } = "";
        public int AlternativeIndex { get; set; } = -1;
    }

    public class SuffixAnalysis
    {
        public string Suffix { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string DetailedDescription { get; set; } = "";
    }

    // Generator va analizator
    public class Analyzer
    {
        private readonly SuffixGrammar _grammar;

        public Analyzer(string grammarFile)
        {
            string grammarText = File.ReadAllText(grammarFile, Encoding.UTF8);

            var lexer = new Lexer(grammarText);

            var tokens = lexer.Tokenize();

            var parser = new Parser(tokens);

            _grammar = parser.Parse() ?? throw new ArgumentNullException(nameof(_grammar));
        }

        public WordAnalysis AnalyzeWord(string word)
        {
            var allAnalyses = AnalyzeWordAllCombinations(word);
            return allAnalyses.FirstOrDefault() ?? new WordAnalysis { OriginalWord = word, Root = word };
        }

        // So'zni qismlarga ajaratish va barcha mavjud kombinatsiyalarini topish
        private List<WordAnalysis> AnalyzeWordAllCombinations(string word)
        {
            var allAnalyses = new List<WordAnalysis>();
            FindAllAnalyses(word, new List<SuffixAnalysis>(), allAnalyses);

            return allAnalyses
                .OrderByDescending(a => a.Suffixes.Count)
                .ThenBy(a => a.Root.Length)
                .ToList();
        }

        // So'zni qismlarga ajratish va mavjud kombinatsiyalar ichidan qoidalarga mos keluvchilarini filtrlash
        public List<WordAnalysis> AnalyzeWordByRules(string word)
        {
            // Barcha kombinatsiyarni topish
            var allAnalyses = AnalyzeWordAllCombinations(word);

            // Qoidalarga mos kelishini aniqlash va filtrlash
            var validAnalyses = new List<WordAnalysis>();

            foreach (var analysis in allAnalyses)
            {
                var matchingRules = FindMatchingRules(analysis);

                foreach (var (ruleName, ruleDesc, ruleId, altIndex) in matchingRules)
                {
                    var validAnalysis = new WordAnalysis
                    {
                        Root = analysis.Root,
                        Suffixes = analysis.Suffixes,
                        OriginalWord = analysis.OriginalWord,
                        MatchedRule = ruleName,
                        RuleDescription = ruleDesc,
                        RuleId = ruleId,
                        AlternativeIndex = altIndex
                    };
                    validAnalyses.Add(validAnalysis);
                }
            }

            // Qoidalarga mos kelishi, to'g'riroq variantlar bo'yicha tartiblash
            return validAnalyses
                .OrderByDescending(a => !string.IsNullOrEmpty(a.MatchedRule))
                .ThenByDescending(a => a.Suffixes.Count)
                .ThenBy(a => a.Root.Length)
                .ToList();
        }

        private List<(string ruleName, string ruleDesc, string ruleId, int altIndex)> FindMatchingRules(WordAnalysis analysis)
        {
            var matches = new List<(string, string, string, int)>();

            foreach (var ruleGroup in _grammar.Rules)
            {
                foreach (var rule in ruleGroup.Value)
                {
                    for (int altIndex = 0; altIndex < rule.Alternatives.Count; altIndex++)
                    {
                        var alternative = rule.Alternatives[altIndex];

                        if (MatchesRuleAlternative(analysis, alternative))
                        {
                            matches.Add((rule.Name, rule.Description, rule.UniqueId, altIndex));
                        }
                    }
                }
            }

            return matches;
        }

        private bool MatchesRuleAlternative(WordAnalysis analysis, List<RuleElement> alternative)
        {
            var suffixIndex = 0;

            foreach (var element in alternative)
            {
                switch (element.Type)
                {
                    case RuleElement.ElementType.Literal:
                        continue;

                    case RuleElement.ElementType.LiteralWithDescription:
                        if (suffixIndex >= analysis.Suffixes.Count)
                            return false;

                        var currentSuffix = analysis.Suffixes[suffixIndex];
                        var suffixKey = currentSuffix.Suffix;

                        if (!element.LiteralMap.ContainsKey(suffixKey))
                            return false;

                        suffixIndex++;
                        break;

                    case RuleElement.ElementType.SuffixSet:
                        if (suffixIndex >= analysis.Suffixes.Count)
                            return false;

                        var suffixSetName = element.Value.TrimStart('@');
                        var setSuffix = analysis.Suffixes[suffixIndex];

                        if (setSuffix.Category != suffixSetName)
                            return false;

                        suffixIndex++;
                        break;

                    case RuleElement.ElementType.Optional:
                        // Majburiy bo'lmagan element - tashlab ketish mumkin
                        if (suffixIndex < analysis.Suffixes.Count && element.Children.Count > 0)
                        {
                            var childElement = element.Children[0];
                            if (childElement.Type == RuleElement.ElementType.SuffixSet)
                            {
                                var optSuffixSetName = childElement.Value.TrimStart('@');
                                var optCurrentSuffix = analysis.Suffixes[suffixIndex];

                                if (optCurrentSuffix.Category == optSuffixSetName)
                                {
                                    suffixIndex++;
                                }
                            }
                            else if (childElement.Type == RuleElement.ElementType.LiteralWithDescription)
                            {
                                var optCurrentSuffix = analysis.Suffixes[suffixIndex];
                                var optSuffixKey = optCurrentSuffix.Suffix;

                                if (childElement.LiteralMap.ContainsKey(optSuffixKey))
                                {
                                    suffixIndex++;
                                }
                            }
                        }
                        break;
                }
            }

            return suffixIndex == analysis.Suffixes.Count;
        }

        private void FindAllAnalyses(string remainingWord, List<SuffixAnalysis> currentSuffixes, List<WordAnalysis> results)
        {
            const int minRootLength = 2;

            if (remainingWord.Length < minRootLength)
                return;

            if (currentSuffixes.Count > 0)
            {
                results.Add(new WordAnalysis
                {
                    OriginalWord = GetOriginalWord(remainingWord, currentSuffixes),
                    Root = remainingWord,
                    Suffixes = new List<SuffixAnalysis>(currentSuffixes)
                });
            }

            if (remainingWord.Length <= minRootLength + 1)
            {
                if (currentSuffixes.Count == 0)
                {
                    results.Add(new WordAnalysis
                    {
                        OriginalWord = remainingWord,
                        Root = remainingWord,
                        Suffixes = new List<SuffixAnalysis>()
                    });
                }
                return;
            }

            var foundMatches = new List<(SuffixAnalysis suffix, string newWord, int priority)>();

            foreach (var suffixSet in _grammar.SuffixSets.Values)
            {
                foreach (var suffixDef in suffixSet.Suffixes.Values)
                {
                    var suffix = suffixDef.Suffix;

                    if (remainingWord.EndsWith(suffix) && remainingWord.Length > suffix.Length)
                    {
                        var wordBeforeSuffix = remainingWord.Substring(0, remainingWord.Length - suffix.Length);

                        if (CheckCondition(wordBeforeSuffix, suffixDef.Condition))
                        {
                            var finalWord = ApplyTransformation(wordBeforeSuffix, suffixDef.Condition);

                            if (finalWord.Length >= minRootLength)
                            {
                                var suffixAnalysis = new SuffixAnalysis
                                {
                                    Suffix = suffix, 
                                    Description = suffixDef.Description,
                                    Category = suffixSet.Name,
                                    DetailedDescription = $"{suffix}:{suffixSet.Description}:{suffixDef.Description}"
                                };

                                var priority = suffix.Length;
                                foundMatches.Add((suffixAnalysis, finalWord, priority));
                            }
                        }
                    }
                }
            }

            var orderedMatches = foundMatches
                .OrderByDescending(m => m.priority)
                .ToList();

            foreach (var (suffixAnalysis, newWord, _) in orderedMatches)
            {
                var newSuffixes = new List<SuffixAnalysis> { suffixAnalysis };
                newSuffixes.AddRange(currentSuffixes);
                FindAllAnalyses(newWord, newSuffixes, results);
            }

            if (currentSuffixes.Count == 0 && foundMatches.Count == 0)
            {
                results.Add(new WordAnalysis
                {
                    OriginalWord = remainingWord,
                    Root = remainingWord,
                    Suffixes = new List<SuffixAnalysis>()
                });
            }
        }

        private string GetOriginalWord(string root, List<SuffixAnalysis> suffixes)
        {
            if (suffixes.Count == 0) return root;

            var result = root;
            foreach (var suffix in suffixes)
            {
                result += suffix.Suffix;
            }
            return result;
        }

        private bool CheckCondition(string word, SuffixCondition condition)
        {
            if (condition.Type == SuffixCondition.ConditionType.None)
                return true;

            if (string.IsNullOrEmpty(word))
                return false;

            switch (condition.Type)
            {
                case SuffixCondition.ConditionType.EndsWith:
                    if (condition.UseRegex)
                    {
                        try
                        {
                            var regex = new Regex(condition.RegexPattern + "$", RegexOptions.IgnoreCase);
                            return regex.IsMatch(word);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new Exception($"Notog'ri regex ifoda '{condition.RegexPattern}': {ex.Message}");
                        }
                    }
                    else
                    {
                        var lastChar = word.Last();
                        return condition.Characters.Contains(lastChar);
                    }

                case SuffixCondition.ConditionType.StartsWith:
                    if (condition.UseRegex)
                    {
                        try
                        {
                            var regex = new Regex("^" + condition.RegexPattern, RegexOptions.IgnoreCase);
                            return regex.IsMatch(word);
                        }
                        catch (ArgumentException ex)
                        {
                            throw new Exception($"Notog'ri regex ifoda '{condition.RegexPattern}': {ex.Message}");
                        }
                    }
                    else
                    {
                        return condition.Characters.Contains(word.First());
                    }

                case SuffixCondition.ConditionType.IsVowel:
                    return Lexer.IsVowel(word.Last());

                case SuffixCondition.ConditionType.IsConsonant:
                    return Lexer.IsConsonant(word.Last());

                default:
                    return true;
            }
        }

        public void PrintAllAnalyses(string word)
        {
            var analyses = AnalyzeWordAllCombinations(word);

            Console.WriteLine($"=== АНАЛИЗ СЛОВА '{word}' ===");
            Console.WriteLine($"Найдено вариантов: {analyses.Count}");
            Console.WriteLine();

            for (int i = 0; i < analyses.Count; i++)
            {
                var analysis = analyses[i];
                var parts = new List<string> { $"\"{analysis.Root}\"" };
                parts.AddRange(analysis.Suffixes.Select(s => s.DetailedDescription));

                Console.WriteLine($"Вариант {i + 1}: {string.Join(" + ", parts)}");
                Console.WriteLine($"  Корень: \"{analysis.Root}\"");

                if (analysis.Suffixes.Count > 0)
                {
                    Console.WriteLine("  Суффиксы:");
                    foreach (var suffix in analysis.Suffixes)
                    {
                        Console.WriteLine($"    +{suffix.Suffix} ({suffix.Category}: {suffix.Description})");
                    }
                }
                Console.WriteLine();
            }
        }

        public List<string> GenerateWordForms(string ruleName, string baseWord)
        {
            if (!_grammar.Rules.ContainsKey(ruleName))
                throw new ArgumentException($"'{ruleName}' nomli qoida topilmadi");

            var rules = _grammar.Rules[ruleName];
            var results = new List<string>();

            foreach (var rule in rules)
            {
                foreach (var alternative in rule.Alternatives)
                {
                    var forms = GenerateFromAlternative(alternative, baseWord);
                    results.AddRange(forms);
                }
            }

            return results.Distinct().ToList();
        }

        private List<string> GenerateFromAlternative(List<RuleElement> elements, string baseWord)
        {
            var results = new List<string> { baseWord };

            foreach (var element in elements)
            {
                var newResults = new List<string>();

                foreach (var currentWord in results)
                {
                    var variations = ProcessElement(element, currentWord);
                    newResults.AddRange(variations);
                }

                results = newResults;
            }

            return results;
        }

        private List<string> ProcessElement(RuleElement element, string word)
        {
            switch (element.Type)
            {
                case RuleElement.ElementType.Literal:
                    if (element.Value.Contains(","))
                    {
                        var options = element.Value.Split(',').Select(s => s.Trim());
                        return options.Select(option => word + option).ToList();
                    }
                    return new List<string> { word + element.Value };

                case RuleElement.ElementType.Optional:
                    var results = new List<string> { word };
                    foreach (var child in element.Children)
                    {
                        results.AddRange(ProcessElement(child, word));
                    }
                    return results;

                case RuleElement.ElementType.SuffixSet:
                    return ProcessSuffixSetReference(element.Value, word);

                default:
                    return new List<string> { word };
            }
        }

        private List<string> ProcessSuffixSetReference(string reference, string word)
        {
            var suffixSetName = reference.TrimStart('@');

            if (!_grammar.SuffixSets.ContainsKey(suffixSetName))
                throw new ArgumentException($"'{suffixSetName}' nomli qo'shimchalar to'plami topilmadi");

            var suffixSet = _grammar.SuffixSets[suffixSetName];
            var results = new List<string>();

            foreach (var suffixDef in suffixSet.Suffixes.Values)
            {
                if (CheckCondition(word, suffixDef.Condition))
                {
                    var modifiedWord = ApplyTransformation(word, suffixDef.Condition);

                    results.Add(modifiedWord + suffixDef.Suffix);
                }
            }

            return results;
        }

        public void PrintRuleBasedAnalyses(string word)
        {
            var analyses = AnalyzeWordByRules(word);

            Console.WriteLine($"===== '{word}' SO'ZINI QOIDALAR BO'YICHA TAHLILI =====");

            var validAnalyses = analyses.Where(a => !string.IsNullOrEmpty(a.MatchedRule)).ToList();
            var invalidAnalyses = analyses.Where(a => string.IsNullOrEmpty(a.MatchedRule)).ToList();

            if (validAnalyses.Count > 0)
            {
                Console.WriteLine($"{validAnalyses.Count} ta to'g'ri variantlar topildi");
                Console.WriteLine();

                for (int i = 0; i < validAnalyses.Count; i++)
                {
                    var analysis = validAnalyses[i];
                    var parts = new List<string> { $"\"{analysis.Root}\"" };
                    parts.AddRange(analysis.Suffixes.Select(s => s.DetailedDescription));

                    Console.WriteLine($"✓ Variant {i + 1}: {string.Join(" + ", parts)}");
                    Console.WriteLine($"  Qoida: {analysis.MatchedRule}" +
                        (string.IsNullOrEmpty(analysis.RuleDescription) ? "" : $" - {analysis.RuleDescription}"));
                    Console.WriteLine($"  Qoida ID: {analysis.RuleId.Substring(0, 8)}...");
                    Console.WriteLine($"  Qator: {analysis.AlternativeIndex + 1}");
                    Console.WriteLine($"  O'zak: \"{analysis.Root}\"");

                    if (analysis.Suffixes.Count > 0)
                    {
                        Console.WriteLine("  Qo'shimchalar:");
                        foreach (var suffix in analysis.Suffixes)
                        {
                            Console.WriteLine($"    {suffix.Suffix} ({suffix.Category}: {suffix.Description})");
                        }
                    }
                    Console.WriteLine();
                }
            }

            if (invalidAnalyses.Count > 0)
            {
                Console.WriteLine($"{invalidAnalyses.Count} ta noto'g'ri variantlar topildi");
                for (int i = 0; i < invalidAnalyses.Count; i++)
                {
                    var analysis = invalidAnalyses[i];
                    var parts = new List<string> { $"\"{analysis.Root}\"" };
                    parts.AddRange(analysis.Suffixes.Select(s => s.DetailedDescription));
                    Console.WriteLine($"✗ {string.Join(" + ", parts)}");
                }
            }
        }

        public void PrintGrammar()
        {
            Console.WriteLine("===== QO'SHIMCHALAR =====");
            foreach (var suffixSet in _grammar.SuffixSets.Values)
            {
                Console.WriteLine($"SUFFIX {suffixSet.Name}" +
                    (string.IsNullOrEmpty(suffixSet.Description) ? "" : $":\"{suffixSet.Description}\""));
                foreach (var suffixDef in suffixSet.Suffixes.Values)
                {
                    var conditionStr = suffixDef.Condition.Type != SuffixCondition.ConditionType.None
                        ? $" WHEN {suffixDef.Condition.Type}"
                        : "";
                    Console.WriteLine($"  {suffixDef.Suffix}: \"{suffixDef.Description}\"{conditionStr}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("===== QOIDALAR =====");
            foreach (var ruleGroup in _grammar.Rules)
            {
                Console.WriteLine($"Qoida '{ruleGroup.Key}':");

                foreach (var rule in ruleGroup.Value)
                {
                    Console.WriteLine($"  RULE {rule.Name}" +
                        (string.IsNullOrEmpty(rule.Description) ? "" : $":\"{rule.Description}\"") +
                        $" (ID: {rule.UniqueId.Substring(0, 8)}...)");

                    for (int i = 0; i < rule.Alternatives.Count; i++)
                    {
                        Console.WriteLine($"   {i + 1}: {string.Join(" + ", rule.Alternatives[i].Select(FormatElement))}");
                    }
                }
                Console.WriteLine();
            }
        }

        private static string FormatElement(RuleElement element) => element.Type switch
        {
            RuleElement.ElementType.Literal => element.Value,
            RuleElement.ElementType.LiteralWithDescription => "{" + string.Join(", ", element.LiteralMap.Select(kv => $"{kv.Key}:\"{kv.Value}\"")) + "}",
            RuleElement.ElementType.Optional => "[" + string.Join(" + ", element.Children.Select(FormatElement)) + "]",
            RuleElement.ElementType.SuffixSet => element.Value,
            _ => element.Value ?? "?"
        };

        private string ApplyTransformation(string word, SuffixCondition condition)
        {

            if (!condition.UseReplace && condition.CutChars == 0)
                return word;

            if (condition.UseReplace)
            {
                if (condition.Type == SuffixCondition.ConditionType.EndsWith && condition.UseRegex)
                {
                    try
                    {
                        var regex = new Regex(condition.RegexPattern + "$", RegexOptions.IgnoreCase);
                        return regex.Replace(word, condition.ReplaceText);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new Exception($"REPLACE qilishda xatolik '{condition.RegexPattern}': {ex.Message}");
                    }
                }
                else if (condition.Type == SuffixCondition.ConditionType.StartsWith && condition.UseRegex)
                {
                    try
                    {
                        var regex = new Regex("^" + condition.RegexPattern, RegexOptions.IgnoreCase);
                        return regex.Replace(word, condition.ReplaceText);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new Exception($"REPLACE qilishda xatolik '{condition.RegexPattern}': {ex.Message}");
                    }
                }
            }
            else if (condition.CutChars > 0 && word.Length >= condition.CutChars)
            {
                return word.Substring(0, word.Length - condition.CutChars);
            }

            return word;
        }
    }
}
