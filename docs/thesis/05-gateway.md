# البوابة — Gateway (API Gateway)

> نقطة الدخول الموحّدة للمنصّة. تستقبل كل طلبات تطبيقات العملاء وتوجّهها إلى الخدمة الخلفية المناسبة دون تعديل المسار، وتجمّع توثيق Swagger لكل الخدمات في واجهة واحدة.

---

## 1. نظرة عامة

### 1.1 الغرض

- **عنوان واحد للعملاء:** تطبيق الجوال ولوحة التحكم يعرفان عنوان البوابة فقط، لا عناوين الخدمات المفردة ومنافذها.
- **توجيه بلا حالة (Stateless routing):** البوابة لا تملك قاعدة بيانات ولا منطق عمل ولا تخزّن جلسات — مجرّد وسيط توجيه (Reverse Proxy) مبني على **Ocelot**.
- **CORS مركزي:** سياسة `AllowAll` مطبَّقة في البوابة (وأيضًا في كل خدمة احتياطيًا).
- **توثيق مُجمَّع:** عبر `MMLib.SwaggerForOcelot` تُدمَج وثائق OpenAPI لكل الخدمات (بما فيها AuthService الخارجية) في واجهة Swagger واحدة على البوابة.

### 1.2 الموقع في المعمارية

| الخاصية | القيمة |
|---|---|
| التقنية | ASP.NET Core (.NET 9) + Ocelot + MMLib.SwaggerForOcelot |
| المنفذ المحلي | `5067` |
| قاعدة البيانات | لا يوجد |
| المصادقة | لا يتحقّق من JWT بنفسه — يمرّره كما هو؛ كل خدمة خلفية تتحقّق محليًا |

### 1.3 اختيار ملف الإعداد حسب البيئة

```
Docker       → ocelot.docker.json
Production    → ocelot.Production.json
غير ذلك      → ocelot.json        (التطوير المحلي)
```

---

## 2. جدول التوجيه (Route Table)

Ocelot يطابق `UpstreamPathTemplate` (ما يطلبه العميل) ويعيد التوجيه إلى `DownstreamHostAndPorts` + `DownstreamPathTemplate` (بنفس المسار). النمط الشائع: مسار «قائمة» صريح (GET/POST) + مسار `{everything}` عام يلتقط البقيّة بالأفعال المسموح بها.

| المسار العلوي (عبر البوابة) | الخدمة الوجهة | المنفذ المحلي | الأفعال |
|---|---|---|---|
| `/api/feedbacks` و `/api/feedbacks/{everything}` | FeedbackService | 5069 | GET, POST, PUT, DELETE |
| `/api/feedbackimages` و `/api/feedbackimages/{everything}` | FeedbackService | 5069 | GET, POST, DELETE |
| `/api/ads` | AdvertisingService | 5000 | GET, POST |
| `/api/ads/{everything}` | AdvertisingService | 5000 | GET, PUT, DELETE |
| `/api/ad-types`، `/api/target-genders` | AdvertisingService | 5000 | GET |
| `/api/notifications` و `/api/notifications/{everything}` | NotificationService | 5081 | GET, POST, PUT, DELETE |
| `/api/buildings`، `/api/buildings/lookup`، `/api/buildings/{id}` | HousingService | 5054 | GET, POST, PUT |
| `/api/buildings/{id}/evacuation/announce` و `/execute` | HousingService | 5054 | POST |
| `/api/buildings/{buildingId}/rooms` و `/rooms/{everything}` | HousingService | 5054 | GET, POST, PUT |
| `/api/housing-cycles` و `/api/housing-cycles/{everything}` | HousingService | 5054 | GET, POST |
| `/api/governorates` و `/api/governorates/{everything}` | HousingService | 5054 | GET, POST, PUT |
| `/api/housing-requests` و `/api/housing-requests/{everything}` | HousingService | 5054 | GET, POST, PUT, DELETE |
| `/api/housing-groups` و `/api/housing-groups/{everything}` | HousingService | 5054 | GET, POST |
| `/api/allocations` و `/api/allocations/{everything}` | HousingService | 5054 | GET, POST |
| `/api/dashboard/housing` | HousingService (`/api/housing-requests/dashboard`) | 5054 | GET |
| `/api/dashboard/feedback` | FeedbackService (`/api/feedbacks/dashboard`) | 5069 | GET |
| `/api/auth/{everything}` | **AuthService** (خارجية) | `university-auth-lemon.vercel.app:443` (HTTPS) | GET, POST, PATCH |
| `/api/admin/{everything}` | **AuthService** (خارجية) | نفسه | GET, POST, PATCH, DELETE |

> `/api/dashboard/housing` و `/api/dashboard/feedback` مساران **مُفتاحَان** (`Key`) يُستخدمان مكوّنَين للتجميعة أدناه، ويصلحان أيضًا لاستدعاء نصف اللوحة وحده.

### 2.1 ملاحظات مهمّة

- **AuthService تُوجَّه أيضًا عبر البوابة** (`/api/auth/*` و `/api/admin/*`) إلى نسخة Vercel المنشورة، في كل ملفات Ocelot الثلاثة (لا نسخة محلية/Docker نتحكّم بها).
- **`/api/internal/*` لا تُوجَّه عبر البوابة عمدًا** — نقاط خادم‑لخادم (بحث المستخدمين، رموز FCM، شحن المحفظة) محميّة بمفتاح داخلي لا برمز مستخدم؛ خدماتنا تستدعيها مباشرةً بعنوان AuthService لا عبر البوابة. تقليل سطح الهجوم بلا فائدة تُذكر من كشفها علنًا.
- البوابة لا تُعدّل المسار: العميل يستخدم نفس مسارات مواصفات OpenAPI بالضبط.

### 2.2 تجميع لوحة الإدارة (`GET /api/dashboard`)

شاشة النظرة العامة في لوحة التحكم تجمع بيانات من خدمتين (السكن + الشكاوى). بدل نداءين من الواجهة، تُعرِّف البوابة **تجميعة Ocelot** واحدة:

| العنصر | القيمة |
|---|---|
| المسار العلوي | `GET /api/dashboard` (GET فقط — التجميع في Ocelot لا يدعم غيره) |
| المكوّنات | `RouteKeys = [ housing-dashboard, feedback-dashboard ]` |
| المُجمِّع | `DashboardAggregator` (صنف مخصّص يُسجَّل بـ `AddSingletonDefinedAggregator`) |

- **السلوك:** البوابة تنادي المسارين الخلفيين بالتوازي وتُمرِّر ترويسة `Authorization` كما هي لكليهما (كلاهما يتطلّب رمز `admin`/`super_admin` ويتحقّق منه محليًا). ثم `DashboardAggregator` **يدمج جسمَي JSON في كائن مسطّح واحد** — مفاتيحهما لا تتقاطع (`pendingRequests`، `occupancyRate`، … من السكن؛ `openComplaints`، `recentFeedback`، … من الشكاوى).
- **معالجة الخطأ:** إن ردّت إحدى الخدمتين بغير `200` (رمز ناقص/منتهٍ مثلًا) يُمرَّر ردّها كما هو بدل جسم نصف مبني.
- مُعرَّف في ملفات Ocelot الثلاثة (محلي/Docker/إنتاج) بعناوين البيئة المناسبة. **ترتيب النشر:** تُنشر HousingService وFeedbackService بالكود الجديد **قبل** البوابة، وإلا يعيد `/api/dashboard` تمرير `404`.

#### مخطّط التسلسل — `GET /api/dashboard`

```
Client                 Gateway                HousingService          FeedbackService
(لوحة التحكم)          (Ocelot)               :5054 (housing)         :5069 (feedback)
   │                      │                        │                       │
   │ GET /api/dashboard   │                        │                       │
   │ Authorization: Bearer <JWT: admin>            │                       │
   │─────────────────────►│                        │                       │
   │                      │ MultiplexingMiddleware — نداءان بالتوازي        │
   │                      │                        │                       │
   │                      │ GET /api/housing-requests/dashboard             │
   │                      │ (Authorization مُمرَّرة كما هي)                  │
   │                      │───────────────────────►│                       │
   │                      │ GET /api/feedbacks/dashboard                    │
   │                      │ (Authorization مُمرَّرة كما هي)                  │
   │                      │───────────────────────────────────────────────►│
   │                      │                        │ تحقّق JWT محلي (RS256) │
   │                      │                        │ استعلامات تجميعية       │
   │                      │                        │ [+ IUserLookup اختياري]│
   │                      │◄───────────────────────│ 200 { مفاتيح السكن }   │
   │                      │                        │                       │ تحقّق JWT محلي
   │                      │                        │                       │ عدّ + أحدث 5 + أسماء
   │                      │◄───────────────────────────────────────────────│ 200 { مفاتيح الشكاوى }
   │                      │                        │                       │
   │             DashboardAggregator.Aggregate([ردّ السكن، ردّ الشكاوى])   │
   │               • أيّ ردّ ≠ 200  ⇒ يُمرَّر كما هو (بلا دمج)              │
   │               • غير ذلك       ⇒ دمج الكائنين في JSON مسطّح واحد         │
   │                      │                        │                       │
   │◄─────────────────────│ 200 { مفاتيح السكن + مفاتيح الشكاوى }           │
   │                      │                        │                       │
```

> البوابة لا تتحقّق من الرمز؛ كلٌّ من HousingService وFeedbackService يتحقّق محليًا (RS256) ويردّ `401` لو غاب/انتهى — وعندها يُمرِّر `DashboardAggregator` ذلك الردّ `401` كما هو.

---

## 3. توثيق Swagger المُجمَّع

- `SwaggerEndPoints` في `ocelot.json` تعرّف مفتاحًا لكل خدمة (`feedback`، `advertising`، `notification`، `housing`، `auth`) مع رابط `swagger.json` الخاص بها.
- كل `Route` يحمل `SwaggerKey` يربطه بالخدمة.
- الواجهة المُجمَّعة على `http://localhost:5067/swagger/index.html`، ومولّد الوثائق على `/swagger/docs`.

---

## 4. النشر

- `Dockerfile` خاص بالبوابة؛ في `docker-compose.yml` تعتمد على إقلاع الخدمات الأربع، وتُنشر ببيئة `Docker` (⇒ `ocelot.docker.json`).
- تُعرَّض على المنفذ `5067:8080`.

---

## 5. قرارات تصميمية (للأطروحة)

| القرار | المبرّر |
|---|---|
| بوابة توجيه بلا منطق عمل ولا مصادقة | فصل المسؤوليات؛ كل خدمة تتحقّق من JWT محليًا (RS256) بلا نداء مركزي لكل طلب — لا نقطة فشل وحيدة للمصادقة. |
| نمط `{everything}` العام مع تقييد الأفعال | إضافة نقاط نهاية جديدة في الخدمة الخلفية لا تتطلّب غالبًا تعديل Ocelot، مع الحفاظ على قصر الأفعال المسموحة لكل مورد. |
| تجميع لوحة الإدارة في البوابة لا في خدمة جديدة | نداء واحد للواجهة عبر حدود خدمتين، دون إدخال اقتران بين HousingService وFeedbackService (كلٌّ يجهل الآخر)؛ البوابة هي المكان الطبيعي لتجميع الاستجابات. |
| توجيه AuthService عبر البوابة أيضًا | عنوان موحّد للعميل حتى للمصادقة؛ لكن مع استثناء `/api/internal/*` صراحةً. |
| ملف Ocelot لكل بيئة | عناوين الخدمات الخلفية تختلف بين المحلي (localhost:منفذ) وDocker (اسم الحاوية:8080) والإنتاج. |
| Swagger مُجمَّع | فريقا لوحة التحكم وتطبيق الجوال يجدان عقود كل الخدمات في مكان واحد لتوليد عملاء HTTP مُنمَّطين. |

---

## 6. القيود والأعمال المستقبلية

- لا تحديد معدّل (Rate Limiting) ولا قاطع دارة (Circuit Breaker) مفعّل حاليًا (Ocelot يدعمهما لكنهما غير مُعدَّين).
- تجميع الاستجابات مُستخدَم في موضع واحد فقط (`GET /api/dashboard`)؛ لا نمط عام للتجميع.
- لا تسجيل مركزي/تتبّع موزّع (Distributed Tracing) على مستوى البوابة.
- سياسة CORS `AllowAll` مناسبة للتطوير؛ يُفترض تضييقها في الإنتاج.
