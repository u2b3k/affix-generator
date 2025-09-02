using System.Text;

namespace AffixGenerator.Generator
{
    public class HunspellConverter
    {
        private readonly SuffixGrammar _grammar;
        private readonly Dictionary<string, char> _flagMapping = new();
        private char _nextFlag = 'A';

        public HunspellConverter(SuffixGrammar grammar)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
        }

        public string ConvertToAff()
        {
            var sb = new StringBuilder();

            // Hunspell aff fayl sarlavhasi
            sb.AppendLine("SET UTF-8");
            sb.AppendLine("LANG uz");
            sb.AppendLine("FLAG long");
            sb.AppendLine();

            // Har bir qo'shimchalar to'plami uchun flag yaratish
            foreach (var suffixSet in _grammar.SuffixSets.Values)
            {
                GenerateSuffixFlags(sb, suffixSet);
            }

            GenerateCompoundRules(sb);

            return sb.ToString();
        }

        private void GenerateSuffixFlags(StringBuilder sb, SuffixSet suffixSet)
        {
            var flag = GetOrCreateFlag(suffixSet.Name);

            var suffixEntries = new List<string>();

            foreach (var suffixDef in suffixSet.Suffixes.Values)
            {
                var entries = GenerateHunspellEntries(suffixDef, suffixSet);

                suffixEntries.AddRange(entries);
            }

            if (suffixEntries.Count > 0)
            {
                sb.AppendLine($"SFX {flag} Y {suffixEntries.Count}");

                foreach (var entry in suffixEntries)
                {
                    sb.AppendLine($"SFX {flag} {entry}");
                }

                sb.AppendLine();
            }
        }

        private List<string> GenerateHunspellEntries(SuffixDefinition suffixDef, SuffixSet suffixSet)
        {
            var entries = new List<string>();
            var suffix = suffixDef.Suffix;
            var condition = suffixDef.Condition;

            // Shartlarni Hunspell ga o'tkazish
            string stripping = "0"; 
            string appending = suffix;
            string conditionPattern = ".";

            if (condition.Type != SuffixCondition.ConditionType.None)
            {
                conditionPattern = ConvertConditionToHunspell(condition);

                // Belgini tashlab yuborish
                if (condition.CutChars > 0)
                {
                    stripping = condition.CutChars.ToString();
                }

                // Belgini almashtirish
                if (condition.UseReplace && !string.IsNullOrEmpty(condition.ReplaceText))
                {
                    if (condition.Type == SuffixCondition.ConditionType.EndsWith)
                    {
                        stripping = "1"; 
                        appending = condition.ReplaceText + suffix;
                    }
                }
            }

            var morphInfo = $"# {suffixSet.Description}:{suffixDef.Description}";

            entries.Add($"{stripping} {appending} {conditionPattern} {morphInfo}");

            return entries;
        }

        private string ConvertConditionToHunspell(SuffixCondition condition)
        {
            switch (condition.Type)
            {
                case SuffixCondition.ConditionType.EndsWith:
                    if (condition.UseRegex)
                    {
                        return ConvertRegexToHunspell(condition.RegexPattern);
                    }
                    else
                    {
                        var chars = string.Join("", condition.Characters);
                        return $"[{chars}]";
                    }

                case SuffixCondition.ConditionType.StartsWith:
                    if (condition.UseRegex)
                    {
                        return ConvertRegexToHunspell(condition.RegexPattern);
                    }
                    else
                    {
                        var chars = string.Join("", condition.Characters);
                        return $"[{chars}]";
                    }

                case SuffixCondition.ConditionType.IsVowel:
                    return "[аеёиоуўэюя]"; // unlilar

                case SuffixCondition.ConditionType.IsConsonant:
                    return "[бвгджзйклмнпрстфхцчшщъьқғҳ]"; // undoshlar

                default:
                    return ".";
            }
        }

        private string ConvertRegexToHunspell(string regexPattern)
        {
            if (regexPattern.StartsWith("[") && regexPattern.EndsWith("]"))
            {
                return regexPattern; 
            }

            if (regexPattern.StartsWith("[^") && regexPattern.EndsWith("]"))
            {
                return regexPattern;
            }

            return regexPattern;
        }

        private void GenerateCompoundRules(StringBuilder sb)
        {
            foreach (var ruleGroup in _grammar.Rules)
            {
                foreach (var rule in ruleGroup.Value)
                {
                    var compoundRule = GenerateCompoundRule(rule);

                    if (!string.IsNullOrEmpty(compoundRule))
                    {
                        sb.AppendLine($"# Rule: {rule.Name} - {rule.Description}");
                        sb.AppendLine(compoundRule);
                        sb.AppendLine();
                    }
                }
            }
        }

        private string GenerateCompoundRule(Rule rule)
        {
            var sb = new StringBuilder();

            foreach (var alternative in rule.Alternatives)
            {
                var flags = new List<string>();

                foreach (var element in alternative)
                {
                    if (element.Type == RuleElement.ElementType.SuffixSet)
                    {
                        var suffixSetName = element.Value.TrimStart('@');

                        var flag = GetOrCreateFlag(suffixSetName);

                        if (element.Type == RuleElement.ElementType.Optional)
                        {
                            flags.Add($"({flag})"); 
                        }
                        else
                        {
                            flags.Add(flag.ToString());
                        }
                    }
                }

                if (flags.Count > 0)
                {
                    sb.AppendLine($"COMPOUNDRULE {string.Join("", flags)}");
                }
            }

            return sb.ToString();
        }

        private char GetOrCreateFlag(string name)
        {
            if (!_flagMapping.ContainsKey(name))
            {
                _flagMapping[name] = _nextFlag++;

                if (_nextFlag > 'Z')
                {
                    _nextFlag = 'a';
                }
                else if (_nextFlag > 'z')
                {
                    _nextFlag = '0';
                }
            }

            return _flagMapping[name];
        }

        public void SaveAffFile(string filePath)
        {
            var affContent = ConvertToAff();

            File.WriteAllText(filePath, affContent, Encoding.UTF8);
        }

        public Dictionary<string, char> GetFlagMapping()
        {
            return new Dictionary<string, char>(_flagMapping);
        }

    }

}
