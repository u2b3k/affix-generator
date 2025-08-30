using AffixGenerator.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AffixGenerator
{
  
    public class Program
    {
        public static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            
            try
            {

                var generator = new Analyzer(@"Rules\uz.txt");

                var word = "олма";

                var wordForms = generator.GenerateWordForms("ot", word);

                Console.WriteLine($"===== '{word}' so'zining qo'shimchalari: =====");

                foreach (var form in wordForms.Take(10))
                {
                    Console.WriteLine($"{form}");
                }

                Console.WriteLine($"Jami {wordForms.Count} ta qo'shimchalar generatsiya qilindi\n");
                
                word = "олмаларимизнинг";

                Console.WriteLine($"===== '{word}' so'zining tahlili: =====");

                foreach (var wa in generator.AnalyzeWordByRules(word).Take(1))
                {
                    Console.Write($"{wa.Root} ");

                    foreach (var suffix in wa.Suffixes)
                    {

                        Console.Write($"{suffix.DetailedDescription} ");
                    }

                    Console.WriteLine();
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Xatolik: {ex.Message}");
            }

            Console.ReadKey();
        }
    }
}