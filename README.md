# affix-generator

O'zbek tilidagi so'zlarga qo'shiladigan qo'shimchalarni generatsiya qiladi.
Qo'shimchalar ketma-ketligi va qoidalari maxsus fayl orqali beriladi.

## Foydalanish

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

## Qoidalar faylining shakli

Qoidalarni ko'rsatuvchi fayl ikkita: SUFFIX va RULE kalit so'zlari orqali beriladi.
Qo'shimchalar to'plami SUFFIX kalit so'z orqali ko'rsatiladi:
```
SUFFIX <suffiks_to'plami_nomi>:<ma'lumot> {
  <qo'shimcha>:<ma'lumot> <shart_va_amal>
}
```
Qoidalar to'plami esa RULE kalit so'z orqali ko'rsatiladi:
```
RULE <qoida_to'plami_nomi>:<ma'lumot> {
  <suffikslar_ketma_ketligi>
}
```

O'zbek tilida so'zga ba'zi qo'shimchalar qo'shilganda o'zidan oldingi bir yoki bir nechta harf o'zgarishga uchrashi mumkinligidan, SUFFIX orqali qo'shimchalar to'plamini kiritayotganda shartli qo'shishdan foydalanish mumkin.
Masalan: "Mushuk"+"gim" (egalik qo'shimchasi) => "Mushugim" misolida, "k" harfi "g" ga aylanadi. Yoki turli harf bilan tugovchi so'zlarda qo'shimchalarning o'zi ham turlicha harf o'zgarishiga uchraydi. Buni SUFFIX orqali qo'shimchalar to'plamini berayotganda shartli ifodadan foydalanib kiritish mumkin:
```
SUFFIX egalik:"ЭГАЛИК" { 
    ғим:"1ШБ" WHEN ENDSWITH /қ/ CUT 1, 
    гим:"1ШБ" WHEN ENDSWITH /к/ CUT 1, 
    йим:"1ШБ" WHEN ENDSWITH /[еоёуўюэ]/, 
    м:"1ШБ" WHEN ENDSWITH /[аия]/, 
    им:"1ШБ" WHEN ENDSWITH /[^аияеоёуўюэқк]/
}
```
Shart qo'shimchadan keyin WHEN kalit so'zi orqali berilishi mumkin va ENDSWITH, ISVOWEL, ISCONSONANT funksiyalari yordamida so'zning oxirgi bir yoki bir nechta harflari shartga mosliligini tekshirish mumkin va undan so'ng REPLACE va CUT kalit so'zlar orqali harflarni boshqa harflarga almashtirish yoki tashlab yuborishni mumkin.
