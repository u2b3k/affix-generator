namespace AffixGenerator.Generator
{
    // Qo'shimchani berishdagi shartlar
    public class SuffixCondition
    {
        // Shart turi
        public enum ConditionType
        {
            None,
            EndsWith,
            StartsWith,
            IsVowel,
            IsConsonant
        }

        public ConditionType Type { get; set; } = ConditionType.None;
        public List<char> Characters { get; set; } = new();
        public string RegexPattern { get; set; } = "";
        public int CutChars { get; set; } = 0;
        public string ReplaceText { get; set; } = "";

        public bool UseRegex => !string.IsNullOrEmpty(RegexPattern);
        public bool UseReplace => !string.IsNullOrEmpty(ReplaceText);
    }

    // Qo'shimchani saqlash uchun
    public class SuffixDefinition
    {
        public string Suffix { get; set; } = "";
        public string Description { get; set; } = "";
        public SuffixCondition Condition { get; set; } = new();
    }

    // Qo'shimchalar to'plami
    public class SuffixSet
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, SuffixDefinition> Suffixes { get; set; } = new();
    }

    // Qoida ichidagi element
    public class RuleElement
    {
        public enum ElementType { Literal, SuffixSet, Optional, LiteralWithDescription }
        public ElementType Type { get; set; }
        public string Value { get; set; } = "";
        public string Description { get; set; } = "";
        public List<RuleElement> Children { get; set; } = new();
        public Dictionary<string, string> LiteralMap { get; set; } = new(); // {миз:"ТС", сиз:"ТС"} kabilar uchun
        public bool IsReference => Value?.StartsWith("@") == true;
    }

    // Qoidani saqlash uchun
    public class Rule
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<List<RuleElement>> Alternatives { get; set; } = new();
        public string UniqueId { get; set; } = Guid.NewGuid().ToString();
    }

    public class SuffixGrammar
    {
        public Dictionary<string, SuffixSet> SuffixSets { get; set; } = new();
        public Dictionary<string, List<Rule>> Rules { get; set; } = new(); // Bir hil nomli qoidalar uchun ro'yxat
    }

    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        private Token CurrentToken => _position < _tokens.Count ? _tokens[_position] : _tokens.Last();
        
        public SuffixGrammar Parse()
        {
            var grammar = new SuffixGrammar();

            while (CurrentToken.Type != TokenType.EOF)
            {
                if (CurrentToken.Type == TokenType.SUFFIX)
                {
                    var suffixSet = ParseSuffixSet();
                    grammar.SuffixSets[suffixSet.Name] = suffixSet;
                }
                else if (CurrentToken.Type == TokenType.RULE)
                {
                    var rule = ParseRuleSet();

                    if (!grammar.Rules.ContainsKey(rule.Name))
                    {
                        grammar.Rules[rule.Name] = new List<Rule>();
                    }
                    grammar.Rules[rule.Name].Add(rule);
                }
                else
                {
                    throw new Exception($"SUFFIX yoki RULE berilishi kerak, biroq {CurrentToken.Type} berilgan, qator nomeri => {CurrentToken.Line}:{CurrentToken.Column}");
                }
            }

            return grammar;
        }

        private SuffixSet ParseSuffixSet()
        {
            Consume(TokenType.SUFFIX);

            var name = Consume(TokenType.IDENTIFIER).Value;
            var description = "";

            // Qo'shimcha izohi
            if (CurrentToken.Type == TokenType.COLON)
            {
                Consume(TokenType.COLON);
                description = Consume(TokenType.STRING).Value;
            }

            Consume(TokenType.LBRACE);

            var suffixes = new Dictionary<string, SuffixDefinition>();

            while (CurrentToken.Type != TokenType.RBRACE)
            {
                var suffixDef = ParseSuffixElement();
                suffixes[suffixDef.Suffix] = suffixDef;

                if (CurrentToken.Type == TokenType.COMMA)
                {
                    Consume(TokenType.COMMA);
                }
                else if (CurrentToken.Type != TokenType.RBRACE)
                {
                    throw new Exception($"',' yoki '}}' berilishi kerak, biroq {CurrentToken.Type} berilgan, qator nomeri => {CurrentToken.Line}:{CurrentToken.Column}");
                }
            }

            Consume(TokenType.RBRACE);

            return new SuffixSet
            {
                Name = name,
                Description = description,
                Suffixes = suffixes
            };
        }

        private SuffixDefinition ParseSuffixElement()
        {
            var suffixName = Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.COLON);
            var description = Consume(TokenType.STRING).Value;

            var condition = new SuffixCondition();

            // Shartni aniqlash
            if (CurrentToken.Type == TokenType.WHEN)
            {
                Consume(TokenType.WHEN);

                if (CurrentToken.Type == TokenType.ENDSWITH)
                {
                    condition.Type = SuffixCondition.ConditionType.EndsWith;
                    Consume(TokenType.ENDSWITH);

                    if (CurrentToken.Type == TokenType.LITERAL_SET)
                    {
                        var chars = Consume(TokenType.LITERAL_SET).Value;
                        condition.Characters = chars.ToList();
                    }
                    else if (CurrentToken.Type == TokenType.REGEX_PATTERN)
                    {
                        condition.RegexPattern = Consume(TokenType.REGEX_PATTERN).Value;
                    }
                }
                else if (CurrentToken.Type == TokenType.STARTSWITH)
                {
                    condition.Type = SuffixCondition.ConditionType.StartsWith;
                    Consume(TokenType.STARTSWITH);

                    if (CurrentToken.Type == TokenType.LITERAL_SET)
                    {
                        var chars = Consume(TokenType.LITERAL_SET).Value;
                        condition.Characters = chars.ToList();
                    }
                    else if (CurrentToken.Type == TokenType.REGEX_PATTERN)
                    {
                        condition.RegexPattern = Consume(TokenType.REGEX_PATTERN).Value;
                    }
                }
                else if (CurrentToken.Type == TokenType.ISVOWEL)
                {
                    condition.Type = SuffixCondition.ConditionType.IsVowel;
                    Consume(TokenType.ISVOWEL);
                }
                else if (CurrentToken.Type == TokenType.ISCONSONANT)
                {
                    condition.Type = SuffixCondition.ConditionType.IsConsonant;
                    Consume(TokenType.ISCONSONANT);
                }

                // CUT va REPLACE ni berish majburiy emas
                if (CurrentToken.Type == TokenType.CUT)
                {
                    Consume(TokenType.CUT);
                    condition.CutChars = int.Parse(Consume(TokenType.NUMBER).Value);
                }
                else if (CurrentToken.Type == TokenType.REPLACE)
                {
                    Consume(TokenType.REPLACE);
                    condition.ReplaceText = Consume(TokenType.STRING).Value;
                }
                // Agar CUT ham REPLACE ham berilmagan bo'lsa, shunchaki qo'shimchani qo'shiladi
            }

            return new SuffixDefinition
            {
                Suffix = suffixName,
                Description = description,
                Condition = condition
            };
        }

        private Rule ParseRuleSet()
        {
            Consume(TokenType.RULE);

            var name = Consume(TokenType.IDENTIFIER).Value;
            var description = "";

            // Qoida izohi
            if (CurrentToken.Type == TokenType.COLON)
            {
                Consume(TokenType.COLON);
                description = Consume(TokenType.STRING).Value;
            }

            Consume(TokenType.LBRACE);

            var alternatives = new List<List<RuleElement>>();

            do
            {
                var alternative = ParseRuleAlternative();
                alternatives.Add(alternative);

                if (CurrentToken.Type == TokenType.COMMA)
                {
                    Consume(TokenType.COMMA);
                }
                else
                {
                    break;
                }

            } while (CurrentToken.Type != TokenType.RBRACE);

            Consume(TokenType.RBRACE);

            return new Rule
            {
                Name = name,
                Description = description,
                Alternatives = alternatives
            };
        }

        private List<RuleElement> ParseRuleAlternative()
        {
            var elements = new List<RuleElement>();

            while (CurrentToken.Type != TokenType.COMMA && CurrentToken.Type != TokenType.RBRACE)
            {
                var element = ParseRuleElement();
                if (element != null)
                    elements.Add(element);

                if (CurrentToken.Type == TokenType.PLUS)
                {
                    Consume(TokenType.PLUS);
                }
            }

            return elements;
        }

        private RuleElement? ParseRuleElement()
        {
            if (CurrentToken.Type == TokenType.LBRACE)
            {
                // {миз:"ТС", сиз:"ТС"}
                Consume(TokenType.LBRACE);
                var literalMap = new Dictionary<string, string>();

                do
                {
                    var literalName = Consume(TokenType.IDENTIFIER).Value;
                    Consume(TokenType.COLON);
                    var literalDesc = Consume(TokenType.STRING).Value;
                    literalMap[literalName] = literalDesc;

                    if (CurrentToken.Type == TokenType.COMMA)
                    {
                        Consume(TokenType.COMMA);
                    }
                    else break;
                } while (true);

                Consume(TokenType.RBRACE);

                return new RuleElement
                {
                    Type = RuleElement.ElementType.LiteralWithDescription,
                    LiteralMap = literalMap
                };
            }
            else if (CurrentToken.Type == TokenType.LBRACKET)
            {
                // To'rtburchak qavslar ichida majburiy bo'lmagan elementlar, masalan [лар]
                Consume(TokenType.LBRACKET);
                var optionalElement = ParseRuleElement();
                Consume(TokenType.RBRACKET);

                return new RuleElement
                {
                    Type = RuleElement.ElementType.Optional,
                    Children = new List<RuleElement> { optionalElement }
                };
            }
            else if (CurrentToken.Type == TokenType.AT)
            {
                // Qo'shimchalar ssilkasi
                Consume(TokenType.AT);
                var refName = Consume(TokenType.IDENTIFIER).Value;

                return new RuleElement
                {
                    Type = RuleElement.ElementType.SuffixSet,
                    Value = "@" + refName
                };
            }
            else if (CurrentToken.Type == TokenType.IDENTIFIER)
            {
                var value = Consume(TokenType.IDENTIFIER).Value;
                return new RuleElement
                {
                    Type = RuleElement.ElementType.Literal,
                    Value = value
                };
            }

            return null;
        }

        private Token Consume(TokenType expectedType)
        {
            if (CurrentToken.Type != expectedType)
            {
                throw new Exception($"{expectedType} berilishi kerak, biroq {CurrentToken.Type} berilgan, qator nomeri => {CurrentToken.Line}:{CurrentToken.Column}");
            }

            var token = CurrentToken;
            _position++;
            return token;
        }

    }
}
