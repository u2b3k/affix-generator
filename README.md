# affix-generator

O'zbek tilidagi so'zlarga qo'shiladigan qo'shimchalarni generatsiya qiladi.
Qo'shimchalar ketma-ketligi va qoidalari maxsus fayl orqali beriladi.

**Qoidalar faylini yuklash:**
```c#
var generator = new Analyzer(@"Rules\uz.txt");
```
**Qo'shimchalarni generatsiya qilish:**
```c#
var word = "олма";
var wordForms = generator.GenerateWordForms("ot", word); // "ot" - qoida nomi
```
**Natija**

| So'zlar      |
|--------------|
| олма         |
| олмами       |
| олмамасми    |
| олмачи       |
| олмадан      |
| олмаданми    |
| олмаданмасми |
| олмаданчи    |
| олманинг     |
| ...          |

**Berilgan so'zni qo'shimchalarga ajratish:**
```c#
var list = generator.AnalyzeWordByRules(word);
```

**Natija**

```олма лар:КЎПЛИК:ЛАР имиз:ЭГАЛИК:1ШК нинг:КЕЛИШИК:ҚАРАТҚИЧ```
