# المعمارية (Microservices) والحاويات (Docker)

> هذا المستند مادّة الأطروحة الخاصّة بالجانب المعماري والتشغيلي: لماذا اختير نمط الخدمات المصغّرة، كيف طُبِّقت مبادئه في هذا النظام تحديدًا، وكيف تُبنى وتُشغَّل الخدمات معًا عبر Docker. للجانب العملي السريع (أوامر يومية) انظر أيضًا `DOCKER.md` في جذر المستودع.

---

## 1. لماذا معمارية الخدمات المصغّرة؟

### 1.1 المقارنة مع المعمارية الأحادية (Monolith)

| المعيار | Monolith (تطبيق واحد) | Microservices (النمط المعتمَد) |
|---|---|---|
| النشر | وحدة واحدة؛ أي تعديل صغير يتطلّب إعادة نشر الكل | كل خدمة تُنشر وتُحدَّث مستقلّة |
| قاعدة البيانات | مخطّط واحد مشترك؛ ترابط قوي بين الوحدات | قاعدة بيانات لكل خدمة؛ حدود بيانات صارمة |
| تقسيم العمل بين المطوّرين | تعارضات دمج متكرّرة على قاعدة كود واحدة | كل عضو فريق يملك خدمة/خدمات مستقلّة |
| العزل عند الخطأ | خطأ في وحدة قد يُسقط التطبيق كلّه | خطأ في خدمة لا يُسقط البقيّة (مع تصميم fail‑safe) |
| القياس (Scaling) | قياس التطبيق كاملًا حتى لو الحِمل على جزء منه | قياس الخدمة المحمَّلة فقط |
| التعقيد التشغيلي | منخفض | أعلى (شبكة، توزيع، اتساق، مراقبة) |

### 1.2 مبرّرات الاختيار في هذا المشروع

1. **مجالات عمل مستقلّة فعليًا:** السكن، الشكاوى، الإعلانات، الإشعارات مجالات منفصلة بقواعد عمل ودورات حياة بيانات لا تتقاطع — تقسيم طبيعي إلى **سياقات محدودة (Bounded Contexts)**.
2. **فريق من عدّة أشخاص (مشروع تخرّج جماعي):** كل عضو يطوّر ويختبر خدمته دون انتظار الآخرين ودون تعارض على قاعدة كود واحدة.
3. **تكامل مع خدمة خارجية جاهزة (AuthService بـ Node.js):** النظام أصلًا موزّع؛ نمط الخدمات المصغّرة يجعل دمج خدمة بتقنية مختلفة أمرًا طبيعيًا لا استثناءً.
4. **إمكانية النشر التدريجي:** تحديث منطق التسكين لا يتطلّب لمس خدمة الإعلانات أو إعادة نشرها.
5. **قابلية القياس الانتقائي مستقبلًا:** خدمة الإشعارات (بثّ لآلاف الطلاب) لها ملف حِمل مختلف تمامًا عن خدمة الشكاوى.

### 1.3 المقايضات المقبولة (Trade‑offs)

| الثمن | كيف عولِج / لماذا قُبِل |
|---|---|
| لا معاملات موزّعة (Distributed Transactions) عابرة للخدمات | التصميم يتجنّب الحاجة إليها: كل عملية ذرّية داخل خدمة واحدة؛ التكامل عبر الخدمات إشعاري/استعلامي فقط، ومصمَّم fail‑safe. |
| اتساق نهائي (Eventual Consistency) للبيانات المشتركة (الأسماء، الأدوار) | مقبول: الأسماء عرض تجميلي؛ نقصها لا يُبطل العملية. |
| تعقيد شبكي وتشغيلي | يُحتوى بالكامل عبر Docker Compose (بيئة موحّدة بأمر واحد). |
| ازدواج شيفرة عابر للخدمات (JWT، الإشعارات، الرفع) | مُستخرَج إلى مكتبة `SharedKernel` واحدة. |
| نداءات HTTP متزامنة قد تُبطئ الاستجابة عند تعطُّل الوجهة | مهلات قصيرة (10 ثوانٍ) + تصميم «لا يرمي استثناء» + عدم اعتماد العملية الأساسية على النداء. |

---

## 2. تطبيق مبادئ الخدمات المصغّرة في هذا النظام

| المبدأ | كيف طُبِّق هنا |
|---|---|
| **سياق محدود لكل خدمة (Bounded Context)** | HousingService (سكن)، FeedbackService (ملاحظات)، AdvertisingService (إعلانات)، NotificationService (إشعارات)، AuthService (هوية) — لا تداخل مجالات. |
| **قاعدة بيانات لكل خدمة (Database per Service)** | `HousingServiceDb` / `FeedbackServiceDb` / `AdvertisingServiceDb` / `NotificationServiceDb` مستقلّة. **لا خدمة تصل إلى جداول خدمة أخرى.** |
| **بيانات لا‑مركزية** | مراجع بين الخدمات **منطقية بلا مفتاح أجنبي فعلي** (`StudentId`، `CollegeId`، `GovernorateId` كنصوص/أعداد فقط). كل خدمة تحفظ ما تحتاجه محليًا (مثال: `HousingRequest.Gender` مُصرَّح ذاتيًا بدل جلبه من AuthService). |
| **نقاط نهاية ذكيّة، أنابيب غبيّة (Smart endpoints, dumb pipes)** | المنطق كلّه في الخدمات؛ البوابة مجرّد موجّه HTTP بلا منطق عمل ولا حالة. |
| **تكامل مقاوم للأعطال (Fail‑safe integration)** | `INotificationPublisher` و`IUserLookupService` **لا يرميان استثناءً**؛ فشل الوجهة يُسجَّل ويُبتلع. |
| **لا‑مركزية الحوكمة التقنية** | خدماتنا بـ .NET، AuthService بـ Node.js — كلٌّ بتقنيته، والتكامل عبر عقود HTTP فقط. |
| **أتمتة البنية التحتية** | `Dockerfile` لكل خدمة + `docker-compose.yml` واحد يشغّل المنظومة كاملة، وترحيلات EF تُطبَّق تلقائيًا عند الإقلاع. |
| **قابلية الملاحظة (Observability)** | تسجيل (Logging) في كل خدمة + `healthcheck` لقاعدة البيانات + توثيق Swagger مُجمَّع على البوابة. |
| **تصميم للفشل (Design for failure)** | مهلات، إعادة محاولة EF (`EnableRetryOnFailure`)، حلّ كسول لبيانات الاعتماد الخارجية، عدم إسقاط العملية عند تعطُّل تابع. |

### 2.1 حدود السياقات (Context Map)

```
┌────────────────────┐        ┌────────────────────┐
│   AuthService       │        │  NotificationService│
│  (Identity Context) │        │ (Messaging Context) │
│  users, roles,      │        │  notifications,     │
│  wallet, FCM tokens │        │  recipients, inbox  │
└─────────┬──────────┘        └──────────▲─────────┘
          │ lookup / wallet /            │ INotificationPublisher
          │ fcm-tokens (X-Internal-Api-Key)   (X-Service-Key)
          │                              │
┌─────────┴──────────────────────────────┴─────────┐
│                                                  │
│  HousingService     FeedbackService     AdvertisingService
│  (Housing Context)  (Feedback Context)  (Advertising Context)
│  buildings, rooms,  feedback, images    ads, targeting
│  requests, groups,
│  decisions, alloc.,
│  cycles, payment
└──────────────────────────────────────────────────┘
        كلٌّ ← DB مستقلّة، تكامل عبر HTTP فقط
```

---

## 3. أنماط الاتصال بين الخدمات

### 3.1 متزامن عبر HTTP/REST فقط

النظام لا يستخدم وسيط رسائل (Message Broker مثل RabbitMQ / Kafka) ولا اتصالًا غير متزامن. كل تكامل نداء HTTP مباشر:

| النداء | المصدر ← الوجهة | الحماية | التزامن |
|---|---|---|---|
| توجيه طلبات العملاء | العميل ← Gateway ← الخدمة | JWT (يُتحقَّق في الخدمة) | متزامن |
| نشر إشعار | خدمة ← NotificationService (`/api/notifications/broadcast`) | `X-Service-Key` | متزامن، fail‑safe |
| بحث أسماء المستخدمين | خدمة ← AuthService (`/api/internal/users/lookup`) | `X-Internal-Api-Key` | متزامن، fail‑safe، دفعات ≤ 200 |
| رموز FCM | NotificationService ← AuthService (`/api/internal/users/fcm-tokens`) | `X-Internal-Api-Key` | متزامن |
| شحن المحفظة (دفع رسوم السكن) | HousingService ← AuthService (`/api/internal/wallet/charge`) | `X-Internal-Api-Key` | متزامن، **ليس fail‑safe** (الطلب يبقى غير مدفوع عند الفشل)، **مُحايِد للتكرار** على `(userId, reference)` |
| تجميع لوحة الإدارة | Gateway ← HousingService + FeedbackService بالتوازي (`/api/dashboard`) | JWT مُمرَّر لكليهما | متزامن، scatter‑gather، دمج الردَّين في جسم واحد |

### 3.2 لماذا لا وسيط رسائل؟

- **البساطة تناسب حجم المشروع:** لا حالات استخدام تتطلّب معالجة غير متزامنة طويلة أو ذروة أحداث عالية.
- **الحتميّة في التطوير والاختبار:** نداء HTTP متزامن أسهل تتبّعًا وتصحيحًا في سياق مشروع تخرّج.
- **التكامل الوحيد «الثقيل» (الإشعارات) مصمَّم fail‑safe** فلا يحتاج ضمان تسليم عبر طابور.
- **مقترح مستقبلي:** إدخال وسيط رسائل للأحداث (`RequestAccepted`، `AllocationCreated`) لفكّ الارتباط الزمني وتحسين المرونة (انظر §9).

### 3.3 تجميع الاستجابات على البوابة — لوحة الإدارة (Scatter‑Gather)

شاشة «النظرة العامة» في لوحة التحكم تحتاج بيانات من سياقين (السكن + الشكاوى). بدل نداءين من الواجهة، تُعرِّف البوابة **تجميعة Ocelot واحدة** (`GET /api/dashboard`، GET فقط):

- **Scatter:** `MultiplexingMiddleware` تنادي `GET /api/housing-requests/dashboard` و`GET /api/feedbacks/dashboard` **بالتوازي**، وتُمرِّر ترويسة `Authorization` كما هي (كلٌّ يتحقّق من الرمز محليًا).
- **Gather:** صنف `DashboardAggregator` مخصّص يدمج جسمَي JSON في **كائن مسطّح واحد** (مفاتيح الخدمتين لا تتقاطع). أيّ ردّ ≠ 200 (رمز ناقص/منتهٍ) يُمرَّر كما هو بدل جسم نصف مبني.
- **قيد ترتيب:** البوابة تُنشر **بعد** الخدمتين الحاملتين لنقطتَي `/dashboard`، وإلا تعيد التجميعة تمرير `404`.
- هذا **الموضع الوحيد** الذي تفعل فيه البوابة أكثر من توجيه مسار كما هو (انظر §4.1) — لا نمط عام للتجميع. تفصيل ومخطّط تسلسل في مستند البوابة (`05-gateway.md` §2.2).

### 3.4 التعامل مع البيانات الموزّعة

- **لا مفاتيح أجنبية عابرة للخدمات** — مستحيلة تقنيًا (قواعد بيانات منفصلة) وغير مرغوبة (اقتران).
- **تكرار مقصود للبيانات (Data Duplication):** ما تحتاجه خدمة لقاعدة عمل صلبة تحفظه محليًا (`HousingRequest.Gender`)، لا تجلبه لحظيًا.
- **إثراء عند القراءة (Read‑time Enrichment):** الأسماء تُجلب من AuthService عند تكوين الاستجابة فقط، بنداء دفعي واحد لكل صفحة (تفادي N+1)، وتُترك `null` عند الفشل.
- **الاتساق مضمون داخل الخدمة فقط:** كل عملية كتابة ذرّية عبر `SaveChangesAsync` واحد أو معاملة صريحة (`CreateWithImagesAsync` في FeedbackService).

---

## 4. البوابة، والاكتشاف، والمصادقة الموزّعة

### 4.1 نمط API Gateway

- **Ocelot** (مكتبة .NET) — موجّه عكسي (Reverse Proxy) بلا حالة.
- عنوان واحد للعميل (`:5067` محليًا / `gateway:8080` داخل Docker)؛ لا يعرف العميل منافذ الخدمات.
- المسار يُمرَّر كما هو (`UpstreamPathTemplate == DownstreamPathTemplate`). **الاستثناء الوحيد:** تجميعة `GET /api/dashboard` التي تُركِّب استجابتَي خدمتين في جسم واحد (§3.3).
- «اكتشاف الخدمات» ثابت (Static): عناوين الوجهات مكتوبة في ملف Ocelot لكل بيئة — لا يوجد Service Registry (Consul/Eureka).

### 4.2 المصادقة اللا‑مركزية

- AuthService وحدها تُصدر رموز JWT موقّعة **RS256**.
- كل خدمة تتحقّق من الرمز **محليًا** مقابل المفتاح العام (`AddSharedJwtAuthentication` في SharedKernel) — **بلا نداء لـ AuthService لكل طلب**.
- البوابة لا تتحقّق من الرمز؛ تمرّره فقط. الفائدة: لا نقطة فشل مركزية للمصادقة، ولا اختناق أداء.
- النداءات خادم‑لخادم لا تستخدم JWT بل سرًّا مشتركًا بترويسة، مع مقارنة زمن‑ثابت (`RequireServiceKeyAttribute`).

---

## 5. الاعتماديات المشتركة — `SharedKernel`

- مكتبة صنفيّة يرجع إليها كل مشروع خدمة (Project Reference)، لا خدمة منشورة.
- توحّد: تحقّق JWT، حماية النداء الداخلي، `INotificationPublisher`، `IUserLookupService`، `IImageUploader`.
- **الخطر المعماري:** المكتبة المشتركة تُدخِل اقترانًا عند البناء (Build‑time coupling) — تعديلها يستلزم إعادة بناء كل الخدمات. خُفِّف بحصر محتواها في **أدوات بنية تحتية بحتة** (لا قواعد عمل، لا كيانات مجال) نادرة التغيّر.
- أثر عملي على Docker: بناء صور الخدمات التي تعتمد SharedKernel يحتاج `context` = جذر المستودع (انظر §7.3).

---

## 6. Docker — المفاهيم والغرض

### 6.1 تعريف موجز

Docker يغلّف الخدمة (الكود + .NET Runtime + المكتبات + الإعدادات) داخل **حاوية (Container)** معزولة، تُبنى من **صورة (Image)** ثابتة، فتعمل بالسلوك نفسه على أي جهاز.

| المفهوم | الدور في هذا المشروع |
|---|---|
| **Image** | قالب تشغيل يُبنى مرّة من `Dockerfile` (خدمة واحدة لكلٍّ). |
| **Container** | نسخة تعمل من الصورة؛ 6 حاويات عند التشغيل (5 خدمات + SQL Server). |
| **Dockerfile** | خطوات بناء صورة خدمة (Multi‑stage). |
| **docker-compose.yml** | يصف الحاويات الستّ وشبكتها وأحجامها ويشغّلها بأمر واحد. |
| **Volume** | تخزين دائم خارج الحاوية (بيانات SQL Server، الملفات المرفوعة). |
| **Network** | شبكة `bridge` داخلية (`university-city`) تتخاطب فيها الحاويات بالاسم. |
| **Healthcheck** | فحص جاهزية SQL Server الفعلية قبل إقلاع الخدمات. |

### 6.2 لماذا Docker لهذا النظام تحديدًا

- **بيئة موحّدة بأمر واحد:** `docker compose up --build` يعطي 5 خدمات + قاعدة بيانات تعمل معًا بنفس الإصدارات — بدل تثبيت SQL Server وتشغيل 5 مشاريع .NET يدويًا وضبط سلاسل الاتصال.
- **عزل الإصدارات:** لا تعارض مكتبات بين الخدمات.
- **شبكة جاهزة:** `Server=sqlserver,1433` بالاسم لا بالـ IP؛ `housingservice:8080` بين الحاويات.
- **ترتيب إقلاع صحيح:** `depends_on: { condition: service_healthy }` يمنع محاولة الاتصال بقاعدة بيانات غير جاهزة.
- **جسر للنشر:** نفس الصورة المُختبَرة محليًا تُرفع لأي خادم إنتاج بلا تعديل.

---

## 7. بناء الصور — تحليل `Dockerfile`

### 7.1 النمط: بناء متعدّد المراحل (Multi‑stage Build)

كل خدمة تتّبع أربع مراحل (مثال Gateway):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base      # (1) runtime خفيف فقط
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build         # (2) SDK كامل (compiler + أدوات)
WORKDIR /src
COPY ["Gateway.csproj", "./"]
RUN dotnet restore "Gateway.csproj"                    #     استعادة الحزم (طبقة قابلة للتخزين المؤقت)
COPY . .
RUN dotnet build "Gateway.csproj" -c Release -o /app/build

FROM build AS publish                                  # (3) نشر مُحسَّن
RUN dotnet publish "Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final                                     # (4) الصورة النهائية = runtime + ملفات النشر فقط
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gateway.dll"]
```

**فائدة الفصل:** المرحلة النهائية تُبنى من `aspnet:9.0` (Runtime فقط) لا `sdk:9.0` — فالصورة الناتجة أصغر وأقلّ سطح هجوم (لا مترجم، لا كود مصدر، لا أدوات بناء). كما أن ترتيب `COPY .csproj` ثم `restore` قبل `COPY . .` يجعل طبقة استعادة الحزم قابلة لإعادة الاستخدام من الذاكرة المؤقتة ما لم تتغيّر ملفات المشروع.

### 7.2 صورة الأساس

- `mcr.microsoft.com/dotnet/aspnet:9.0` للتشغيل، `mcr.microsoft.com/dotnet/sdk:9.0` للبناء — صور Microsoft الرسمية.
- كل خدمة تُصغي على المنفذ `8080` داخل الحاوية (`EXPOSE 8080`)، وهو المنفذ القياسي لـ ASP.NET على Docker.

### 7.3 الاختلاف: خدمات تعتمد `SharedKernel`

| الخدمة | `context` في compose | نمط `COPY` في Dockerfile | السبب |
|---|---|---|---|
| Gateway | `./Gateway` | `COPY ["Gateway.csproj", "./"]` ثم `COPY . .` | لا اعتماد على SharedKernel — يكفي مجلد الخدمة. |
| Feedback / Advertising / Notification / Housing | `.` (جذر المستودع) | `COPY . .` مباشرةً قبل `restore` | تعتمد `SharedKernel` كمشروع مرجعي؛ يحتاج البناء رؤية مجلد `SharedKernel` خارج مجلد الخدمة، لذا يجب أن يكون سياق البناء هو جذر المستودع. |

`.dockerignore` يستبعد `bin/`، `obj/`، `.vs/`، `wwwroot/uploads/`، `.git/` من سياق البناء (تسريع النقل وتصغير الطبقات).

---

## 8. التشغيل المتكامل — تحليل `docker-compose.yml`

### 8.1 الخدمات الستّ

| الحاوية | الصورة/البناء | المنفذ (مضيف:حاوية) | يعتمد على | أحجام |
|---|---|---|---|---|
| `sqlserver` | `mcr.microsoft.com/mssql/server:2022-latest` | `1433:1433` | — | `sqlserver-data` |
| `feedbackservice` | `FeedbackService/Dockerfile` (context `.`) | `5069:8080` | `sqlserver` (healthy) | `feedback-uploads` |
| `advertisingservice` | `AdvertisingService/Dockerfile` (context `.`) | `5000:8080` | `sqlserver` (healthy) | `advertising-uploads` |
| `notificationservice` | `NotificationService/Dockerfile` (context `.`) | `5081:8080` | `sqlserver` (healthy) | — |
| `housingservice` | `HousingService/HousingService/Dockerfile` (context `.`) | `5054:8080` | `sqlserver` (healthy) | — |
| `gateway` | `Gateway/` (context `./Gateway`) | `5067:8080` | الخدمات الأربع | — |

### 8.2 آليات مفتاحية

**أ) فحص الصحّة وترتيب الإقلاع**
```yaml
sqlserver:
  healthcheck:
    test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q 'SELECT 1' || exit 1"]
    interval: 10s ; timeout: 5s ; retries: 10
...
housingservice:
  depends_on:
    sqlserver: { condition: service_healthy }
```
الخدمة لا تبدأ إلا بعد أن يُعلن SQL Server جاهزيته الفعلية لاستقبال اتصالات (لا مجرّد أن الحاوية «تعمل») — يمنع أخطاء «فشل الاتصال بقاعدة البيانات» عند الإقلاع البارد.

**ب) الاتصال بقاعدة البيانات بالاسم الشبكي**
```yaml
environment:
  ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=HousingServiceDb;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;"
```
- `Server=sqlserver` = اسم الخدمة على شبكة `university-city` (لا `localhost`).
- **قاعدة بيانات منطقية منفصلة لكل خدمة** داخل نفس خادم SQL (`HousingServiceDb`، `FeedbackServiceDb`، ...) — يحافظ على مبدأ «DB per service» منطقيًا مع خادم واحد للتبسيط في بيئة التطوير.
- صيغة `ConnectionStrings__DefaultConnection` (شرطتان سفليّتان) هي تعيين متغيّر بيئة إلى مفتاح إعداد متداخل في .NET.

**ج) التواصل بين الخدمات عبر متغيّرات البيئة**
```yaml
advertisingservice:
  environment:
    NOTIFICATION_SERVICE_BASE_URL: "http://notificationservice:8080"
    INTERNAL_SERVICE_KEY: ${INTERNAL_SERVICE_KEY}
notificationservice:
  environment:
    INTERNAL_SERVICE_KEY: ${INTERNAL_SERVICE_KEY}
    FIREBASE_SERVICE_ACCOUNT_JSON: ${FIREBASE_SERVICE_ACCOUNT_JSON:-}
```
`INTERNAL_SERVICE_KEY` **نفس القيمة** على الخدمة المستدعِية والمُستدعاة (سرّ `X-Service-Key` المشترك).

الخدمات التي تنادي AuthService داخليًا (HousingService وFeedbackService لبحث الأسماء، وHousingService إضافةً لشحن المحفظة عند الدفع) تحتاج أيضًا `AUTH_SERVICE_BASE_URL` و`AUTH_SERVICE_INTERNAL_API_KEY` (ترويسة `X-Internal-Api-Key`) — **مفتاح واحد** يخدم بحث الأسماء وشحن المحفظة معًا.

**د) الشبكة والأحجام**
```yaml
networks: { university-city: { driver: bridge } }
volumes: { sqlserver-data: , feedback-uploads: , advertising-uploads: }
```
- شبكة `bridge` واحدة تربط كل الحاويات؛ لا حاجة لفتح منافذ على المضيف للتخاطب الداخلي.
- `sqlserver-data` يُبقي بيانات القاعدة على القرص عبر إعادة إنشاء الحاوية؛ `*-uploads` للملفات المرفوعة تاريخيًا (اليوم الرفع إلى Cloudinary، فهذه الأحجام إرث انتقالي).

**هـ) الأسرار خارج الملف**
`MSSQL_SA_PASSWORD`، `INTERNAL_SERVICE_KEY`، `FIREBASE_SERVICE_ACCOUNT_JSON` تُقرأ من ملف `.env` محلي (غير مرفوع على Git؛ النموذج في `.env.example`) — لا تُكتب في `docker-compose.yml`.

### 8.3 مخطّط النشر (Deployment Diagram)

```
                       Docker Host
┌──────────────────────────────────────────────────────────────┐
│  network: university-city (bridge)                            │
│                                                              │
│  ┌────────────┐ 5067:8080                                     │
│  │  gateway    │◄───────────────────── العميل (متصفّح / جوال)  │
│  └──┬──┬──┬──┬─┘                                              │
│     │  │  │  │  http://<service>:8080                         │
│  ┌──▼┐┌▼─┐┌▼─┐┌▼────────┐                                     │
│  │fb ││ad││nt││housing   │  (كلٌّ 8080 داخليًا، ومنفذ مضيف للتصحيح)│
│  └─┬─┘└┬─┘└┬─┘└────┬─────┘                                    │
│    │   │   │       │  Server=sqlserver,1433                   │
│    └───┴───┴───────┴──────────┐                               │
│                          ┌────▼─────┐  volume: sqlserver-data │
│                          │ sqlserver │  (DBs: Feedback/Advert/ │
│                          └───────────┘   Notification/Housing) │
└──────────────────────────────────────────────────────────────┘
        │ (خروج للإنترنت)
        ▼
  AuthService (Vercel)   •   Cloudinary   •   Firebase Cloud Messaging
```

### 8.4 تسلسل طلب نموذجي (تقديم طلب تسكين + إشعار)

```
العميل → Gateway:5067  POST /api/housing-requests  (Bearer JWT)
Gateway → housingservice:8080  (نفس المسار، بلا تعديل)
housingservice: يتحقّق JWT محليًا (RS256) → يرفع الوثائق إلى Cloudinary
              → INSERT في HousingServiceDb (SaveChanges ذرّي)
              → [لاحقًا عند القرار] INotificationPublisher.NotifyUserAsync(...)
                 → POST notificationservice:8080/api/notifications/broadcast (X-Service-Key)
                    notificationservice → AuthService /api/internal/users/lookup (رموز FCM)
                                        → Firebase FCM (دفع)
                                        → INSERT NotificationRecipients
              ← (فشل أي نداء تابع لا يُسقط إنشاء/قرار الطلب)
housingservice → Gateway → العميل : 201 Created + RequestId
```

### 8.5 تسلسل دفع رسوم السكن (تكامل المحفظة — ليس fail‑safe)

```
العميل → Gateway:5067  POST /api/housing-requests/{id}/pay  (Bearer JWT: student)
Gateway → housingservice:8080  (نفس المسار)
housingservice: يتحقّق JWT محليًا → حرّاس: الطلب موجود، المستدعي مالكه،
                غير مدفوع، قراره Accepted، HousingFeeAmount > 0
              → POST AuthService /api/internal/wallet/charge  (X-Internal-Api-Key)
                 { userId, amount, reference: "housing-request-{id}", description }
                 مُحايِد للتكرار: نداء مُعاد بنفس المرجع يعيد 200 بالرصيد الأصلي بلا خصم ثانٍ
   ┌── 200 { data.balance } ──► IsPaid = true, PaidAt = now (SaveChanges ذرّي)
   │                          → INotificationPublisher: "تم دفع رسوم السكن"
   ├── 402 ────────────────────► الطلب يبقى غير مدفوع → 402 "رصيدك لا يكفي"
   └── 404/401/5xx ────────────► استثناء يُلتقط → 502، الطلب يبقى غير مدفوع
housingservice → Gateway → العميل : 200 / 402 / 409 / 502
```

> خلاف نداء الإشعار (fail‑safe، يُبتلع الفشل)، نداء شحن المحفظة **يجب** أن يُبلِّغ المستدعي بدقّة بنتيجته — تحريك المال ليس تكاملًا اختياريًا. المرونة هنا عبر **حِياد التكرار** لا عبر «تجاهل الفشل».

---

## 9. بيئات النشر الثلاث

| البيئة | ملف Ocelot | عناوين الوجهات | ملاحظات |
|---|---|---|---|
| تطوير محلي | `ocelot.json` | `localhost:5069/5000/5081/5054` | 5 مشاريع تعمل بمنافذ مختلفة على الجهاز |
| Docker | `ocelot.docker.json` | `feedbackservice:8080` ... `gateway:8080` (BaseUrl) | أسماء الخدمات على شبكة Compose |
| إنتاج | `ocelot.Production.json` | عناوين المضيفين المنشورين | — |

- الاختيار في `Program.cs` عبر `builder.Environment.EnvironmentName` (`Docker` ⇐ `ASPNETCORE_ENVIRONMENT: Docker` في compose).
- `/api/auth/*` و`/api/admin/*` تشير في **الملفات الثلاثة** إلى نفس نسخة AuthService المنشورة على Vercel (لا نسخة محلية/Docker نتحكّم بها).
- توثيق Swagger المُجمَّع يشير في نسخة Docker إلى `http://<service>:8080/swagger/v1/swagger.json`.

---

## 10. المرونة والجاهزية التشغيلية (Resilience)

| الآلية | الموضع | الغرض |
|---|---|---|
| `EnableRetryOnFailure(5, 10s)` + `CommandTimeout(60)` | `AddDbContext` في كل خدمة | تحمّل انقطاعات SQL Server العابرة (خصوصًا على استضافة مجانية) |
| مهلة HttpClient 10 ثوانٍ | نداءات AuthService / NotificationService | منع تعليق الطلب عند تعطُّل الوجهة |
| تصميم «لا يرمي استثناء» | `INotificationPublisher`، `IUserLookupService` | فشل التابع لا يُسقط العملية الأساسية |
| حِياد التكرار (Idempotency) على `(userId, reference)` | نداء شحن المحفظة (الدفع) | نداء دفع مُعاد/متزامن بنفس المرجع لا يخصم مرّتين — مرونة بديلة عن قفل موزّع، لتكاملٍ لا يصحّ ابتلاع فشله |
| تمرير الردّ ≠ 200 كما هو في التجميع | `DashboardAggregator` على البوابة | لوحة الإدارة لا تُخفي فشل جزء خلف جسم نصف مبني |
| حلّ كسول لبيانات الاعتماد | Cloudinary، Firebase | الخدمة تُقلِع وتخدم نقاطها غير المعتمِدة قبل توفير المفاتيح |
| `Database.MigrateAsync()` عند الإقلاع | `Program.cs` كل خدمة | مخطّط القاعدة دائمًا محدَّث مع الكود المنشور |
| `healthcheck` + `depends_on` | compose | ترتيب إقلاع صحيح |
| مقارنة زمن‑ثابت للأسرار | `RequireServiceKeyAttribute` | تحصين ضد هجمات التوقيت |

---

## 11. أوامر Docker المرجعية

```bash
docker compose up --build          # بناء وتشغيل كل الخدمات
docker compose up -d --build       # في الخلفية
docker compose ps                  # حالة الحاويات
docker compose logs -f housingservice   # متابعة لوجات خدمة
docker compose down                # إيقاف
docker compose down -v             # إيقاف + حذف الأحجام (يمسح قاعدة البيانات المحلية)
```

قبل أول تشغيل: نسخ `.env.example` إلى `.env` وملء `MSSQL_SA_PASSWORD` و`INTERNAL_SERVICE_KEY` (و`FIREBASE_SERVICE_ACCOUNT_JSON` عند الحاجة للدفع).

---

## 12. تقييم المعمارية — نقاط للأطروحة

### 12.1 ما تحقّق فعليًا

- فصل نظيف لخمسة سياقات مستقلّة، كل بقاعدة بياناته وترحيلاته وتوثيقه.
- نقطة دخول موحّدة، ومصادقة موزّعة بلا اختناق مركزي.
- تكامل بين الخدمات مقاوم للأعطال (fail‑safe) في المسارات غير الحرجة، ومُحايِد للتكرار في المسار الحرِج الوحيد (الدفع).
- تجميع استجابات عابر لسياقين على البوابة (`/api/dashboard`) دون إدخال اقتران بين الخدمتين.
- بيئة تطوير/اختبار كاملة بأمر واحد عبر Docker Compose.
- إمكانية النشر المستقل لكل خدمة.

### 12.2 القيود الحاليّة

| القيد | الأثر |
|---|---|
| خادم SQL Server واحد لكل قواعد البيانات | «DB per service» منطقيًا لا فيزيائيًا؛ نقطة فشل بنيوية مشتركة في هذه الإعدادات. |
| لا وسيط رسائل | التكاملات مقترنة زمنيًا؛ لا إعادة تسليم مضمونة للإشعارات. |
| اكتشاف خدمات ثابت (ملفات Ocelot) | تغيير طوبولوجيا النشر يتطلّب تعديل ملف. |
| تجميعة `/api/dashboard` تعتمد ترتيب نشر (الخدمتان قبل البوابة) وتدعم GET فقط | نقطة تجميع واحدة يدويّة؛ لا نمط عام قابل لإعادة الاستخدام. |
| `SharedKernel` = اقتران وقت البناء | تعديلها يستلزم إعادة بناء كل الخدمات. |
| لا تتبّع موزّع مركزي (Distributed Tracing) ولا تجميع لوجات | تصحيح سيناريو عابر لعدّة خدمات يدوي. |
| لا CI/CD ولا تنسيق حاويات (K8s) | البناء والنشر يدويان. |
| CORS = `AllowAll` | مناسب للتطوير؛ يجب تضييقه في الإنتاج. |

### 12.3 مقترحات تطوير مستقبلية

1. **وسيط رسائل (RabbitMQ/Kafka)** لأحداث المجال (`RequestAccepted`، `AllocationCreated`، `MemberLeftGroup`) — فكّ الارتباط الزمني + ضمان التسليم.
2. **نسخة قاعدة بيانات لكل خدمة فعليًا** (حاوية/خادم مستقل) لإزالة نقطة الفشل المشتركة.
3. **CI/CD** (GitHub Actions): بناء ودفع الصور تلقائيًا + تشغيل `HousingService.Tests` عند كل دفع.
4. **تنسيق حاويات** (Docker Swarm / Kubernetes) للقياس الأفقي والتعافي الذاتي.
5. **ملاحظة مركزية:** تجميع لوجات (ELK/Seq) + تتبّع موزّع (OpenTelemetry + Jaeger) + مقاييس (Prometheus/Grafana).
6. **اكتشاف خدمات ديناميكي** (Consul) بدل عناوين Ocelot الثابتة.
7. **قاطع دارة وتحديد معدّل** على البوابة (Ocelot يدعمهما) لعزل الخدمات المتعثّرة.
8. **إخراج `SharedKernel` كحِزَم NuGet مُصدَّرة** لفكّ اقتران وقت البناء (كل خدمة تثبّت إصدارًا).
9. **إدارة أسرار** (Docker secrets / Vault) بدل ملف `.env`.
