# دليل استكشاف أخطاء آلية Cache في أداة Bitwarden

## المشكلة المبلغ عنها
- البحث عن "bw github" يستغرق 7 ثوانٍ
- إعادة البحث عن نفس القيمة يستغرق أيضاً 7 ثوانٍ
- آلية Cache لا تعمل كما متوقع

## التحسينات المطبقة

### 1. ترتيب Cache محسن
```
الترتيب الجديد:
1. Quick Search Cache (5 دقائق) - للبحثات الأخيرة
2. Vault Item Cache (48 ساعة) - للعناصر المحفوظة
3. CLI Search - كملاذ أخير
```

### 2. تحسين Logging
أضفت رموز تعبيرية ورسائل واضحة لتتبع:
- ✅ QUICK CACHE HIT
- 🗂️ VAULT CACHE  
- 🔍 CLI SEARCH
- 💾 Cache Updates

## كيفية اختبار آلية Cache

### خطوة 1: تمكين Debug Logging
1. افتح إعدادات الأداة في Flow Launcher
2. فعّل "Log Debug" و "Log Info"
3. احفظ الإعدادات

### خطوة 2: مراقبة Log Files
يجب أن تشاهد رسائل مثل:
```
🔍 SearchBitwardenAsync called with term: 'bw github'
ℹ️ QUICK CACHE MISS: No entry found for 'bw github'
🗂️ VAULT CACHE: Found X results for 'bw github'
✅ VAULT CACHE HIT: Returning X results for 'bw github'
💾 Saved X results to quick cache for 'bw github'
```

### خطوة 3: اختبار Cache
1. **البحث الأول**: `bw github`
   - متوقع: 7 ثوانٍ (CLI search)
   - Log: `🔍 CLI SEARCH for 'bw github'`

2. **البحث الثاني**: `bw github` (فوراً)
   - متوقع: <100ms (Quick cache hit)
   - Log: `✅ QUICK CACHE HIT: Found X results for 'bw github'`

3. **بعد 5 دقائق**: `bw github`
   - متوقع: <200ms (Vault cache hit)
   - Log: `✅ VAULT CACHE HIT: Returning X results for 'bw github'`

## أسباب محتملة لعدم عمل Cache

### 1. Debug Logging معطل
**الحل**: فعّل Debug و Info logging في الإعدادات

### 2. Vault Cache فارغ أو منتهي الصلاحية
**التشخيص**: ابحث عن:
```
❌ Vault cache is empty
🗂️ VAULT CACHE: Invalid or empty
```

### 3. Session Key مشاكل
**التشخيص**: ابحث عن:
```
No valid session key found
```

### 4. Cache Files تالفة
**الحل**: احذف ملفات Cache:
```
%APPDATA%\FlowLauncher\Plugins\Bitwarden-X.X.X\VaultItemCache\
%APPDATA%\FlowLauncher\Plugins\Bitwarden-X.X.X\FaviconCache\
```

## اختبار مفصل للـ Cache

### اختبار Quick Search Cache (5 دقائق)
```
1. ابحث: "bw github" (7 ثوانٍ متوقع)
2. انتظر ثانيتين
3. ابحث: "bw github" مرة أخرى (فوري متوقع)
4. ابحث: "bw google" (7 ثوانٍ متوقع)
5. ابحث: "bw github" (فوري متوقع)
6. ابحث: "bw google" (فوري متوقع)
```

### اختبار Vault Cache (48 ساعة)
```
1. انتظر أكثر من 5 دقائق
2. ابحث: "bw github" (<500ms متوقع)
```

## علامات نجاح Cache

### Quick Cache يعمل:
- البحث المتكرر فوري (<100ms)
- Log يظهر: `✅ QUICK CACHE HIT`

### Vault Cache يعمل:
- البحث بعد 5+ دقائق سريع (<500ms)
- Log يظهر: `✅ VAULT CACHE HIT`

### كلاهما يعمل:
- أول بحث: 7 ثوانٍ
- خلال 5 دقائق: فوري
- بعد 5 دقائق: سريع

## خطوات استكشاف الأخطاء

1. **فعّل Debug Logging**
2. **امسح Cache** (إذا لزم الأمر)
3. **أعد تشغيل Flow Launcher**
4. **اختبر البحث وراقب اللوج**
5. **شارك اللوج** إذا لم تعمل آلية Cache

## ملفات Log
موقع ملفات اللوج:
```
%APPDATA%\FlowLauncher\Logs\
```
