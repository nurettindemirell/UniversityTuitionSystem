# University Tuition System

Bu proje üniversite harç yönetimi için **sadece backend API’lerinden** oluşan bir sistemdir.  
her şey **Swagger** ve/veya **API çağrıları** üzerinden test ediliyor.

Desteklenen client’lar:
- University Mobile App
- Banking App
- University Web Admin
- Hepsi bir**API Gateway**üzerinden yönetilebilecek şekilde tasarlandı.
---
## 1. Teknolojiler
- **.NET 8** – ASP.NET Core Web API
- **Entity Framework Core**
  - DEBUG: InMemory provider
  - RELEASE: Azure SQL (UseSqlServer, `DefaultConnection`)
- **JWT Authentication (Bearer)**
- **CSVHelper** – Batch tuition import için
- **API Gateway**
  - Reverse proxy (`HttpClient`)
  - Rate limiting
  - Logging
  - 
Proje yapısı (solution klasörü):
- `University.Api`
- `University.Gateway`
-
## 2. Data Model (ER)

### Entity’ler

**Student**

- `Id` (int, PK)
- `StudentNo` (string, unique mantığında)
- `FullName` (string, nullable)

**Tuition**

- `Id` (int, PK)
- `StudentId` (FK → Student.Id)
- `Term` (string, örn: "2024-FALL")
- `TotalAmount` (decimal)
- `Balance` (decimal)

**Payment**

- `Id` (int, PK)
- `StudentId` (FK → Student.Id)
- `Term` (string)
- `Amount` (decimal)
- `PaymentDate` (DateTime)

### İlişkiler

- 1 Student → N Tuition
- 1 Student → N Payment
---

## 3. Versioning
Tüm servisler **v1** ile versiyonlanmıştır. Route’lar:
- `/api/v1/auth/...`
- `/api/v1/admin/...`
- `/api/v1/banking/...`
- `/api/v1/mobile/...`
"REST services must be versionable" şartı bu şekilde karşılandı.
---

## 4. Endpoint’ler ve Requirements Eşlemesi

### 4.1 Auth
**Controller:** `AuthController`  
**Route base:** `/api/v1/auth`  
#### POST `/api/v1/auth/login`

- **Body (JSON):**
  ```json  //////////////////burada banker,password da mevcut
  {
    "username": "admin",
    "password": "password"
  }
  ```
- Kullanıcı doğrulaması basit ve sabit:

  - `username == "admin"`
  - `password == "password"`

- Doğruysa:
  - 1 saat geçerli bir **JWT token** üretir.
  - Response: `200 OK`+ body’de düz string token
- Yanlışsa:
  - `401 Unauthorized` atıldı

Bu token, diğer protected endpoint’ler için  
`Authorization: Bearer <token>` header’ı ile kullanılır.

--

### 4.2 University Web Admin (AdminTuition)

**Controller:** `AdminTuitionController`  
**Base route:** `/api/v1/admin/tuition`  
**Authentication:** **JWT zorunlu** (`[Authorize]`)

#### 4.2.1 Add Tuition – `POST /api/v1/admin/tuition`

Requirements’teki "Add TuitionSingle" kısmı.

- **Body:**
  ```json
  {
    "studentNo": "S123",
    "term": "2024-FALL",
    "totalAmount": 1000.0
  }
  ```

- Davranış:
  - `StudentNo`’ya göre öğrenci arar.
    - Yoksa yeni `Student` oluşturur.
  - Aynı `studentNo + term` için daha önce tuition eklenmişse:
    - `400 BadRequest`  
      ```json
      {
        "status": "Error",
        "message": "Tuition already exists for student and term"
      }
      ```
  - Yoksa yeni `Tuition` kaydı ekler (`Balance = TotalAmount`).

- **Başarılı response:**
  ```json
  {
    "status": "Successful",
    "message": "Tuition added"
  }
  ```


#### 4.2.2 Add Tuition – Batch – `POST /api/v1/admin/tuition/batch`

Requirements’teki "Add Tuition – Batch" kısmı.

- **Request:**
  - `multipart/form-data`
  - Dosya field adı: `file`
  - CSV formatı (header dahil):

    ```csv
    StudentNo,Term,TotalAmount
    S123,2024-FALL,1000
    S124,2024-FALL,1200
    ```

- Davranış:
  - `CsvHelper` kullanılarak satırlar okunur.
  - Her satır için:
    - Student yoksa oluşturur.
    - Aynı student+term tuition varsa **atlar** (hata saymaz).
    - Yoksa yeni tuition ekler.
  - En sonda kaç kayıt eklendiği raporlanır.

- **Örnek response:**
  ```json
  {
    "status": "Successful",
    "message": "Batch completed. Added 2 records."
  }
  ```

#### 4.2.3 Unpaid Tuition Status – `GET /api/v1/admin/tuition/unpaid`

Requirements’teki "Unpaid Tuition Status" + **Paging** kısmı.

- **Authentication:** YES (JWT)
- **Query parametreleri:**
  - `term` (zorunlu) – örn: `2024-FALL`
  - `pageNumber` (opsiyonel, default = 1)
  - `pageSize` (opsiyonel, default = 20)
- **Davranış:**
  - İlgili term için `Balance > 0` olan tüm tuition kayıtlarını bulur.
  - `pageNumber` ve `pageSize` ile sayfalama yapar.

- **Response tipi:**
  ```json
  {
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 3,
    "items": [
      {
        "studentNo": "S123",
        "fullName": "Test Student",
        "term": "2024-FALL",
        "balance": 500.0
      }
    ]
  }
  ```

Paging requirement’ı bu endpoint üzerinde sağlanmış durumda.

---

### 4.3 Banking App

**Controller:** `BankingController`  
**Base route:** `/api/v1/banking`

#### 4.3.1 Query Tuition – `GET /api/v1/banking/tuition`

- **Authentication:** YES (`[Authorize]`)
- **Query parametre:**
  - `studentNo`
- **Response:**
  ```json
  {
    "tuitionTotal": 1000.0,
    "balance": 500.0
  }
  ```


Bu, requirements’teki "Banking App – Query Tuition – Auth: YES" satırını karşılıyor.

#### 4.3.2 Pay Tuition – `POST /api/v1/banking/pay`

- **Authentication:** NO (`[AllowAnonymous]`) – tabloya göre.
- **Body:**
  ```json
  {
    "studentNo": "S123",
    "term": "2024-FALL",
    "amount": 500.0
  }
  ```


- Davranış:
  - `studentNo` ve `term` üzerinden tuition bulur.
  - Yoksa hata döner.
  - Varsa:
    - `Payment` tablosuna kayıt ekler (`PaymentDate = UtcNow`).
    - `Tuition.Balance`’ı `amount` kadar azaltır (0 altına düşmeyecek şekilde).

- **Başarılı response:**
  ```json
  {
    "status": "Successful",
    "message": "Payment processed"
  }
  ```


- **Hata örneği:**
  ```json
  {
    "status": "Error",
    "message": "Tuition not found"
  }
  ```

---

### 4.4 Mobile App

**Controller:** `MobileController`  
**Base route:** `/api/v1/mobile`

#### 4.4.1 Query Tuition – `GET /api/v1/mobile/tuition`

- **Authentication:** NO (`[AllowAnonymous]`)
- **Rate limiting:** API Gateway seviyesinde (aşağıda).
- **Query parametre:**
  - `studentNo`
- **Davranış:**
  - İlgili öğrencinin tüm `Tuition` kayıtlarını toplar:
    - `TuitionTotal = Sum(TotalAmount)`
    - `Balance = Sum(Balance)`
- **Response:**
  ```json
  {
    "tuitionTotal": 1000.0,
    "balance": 500.0
  }
  ```

Mobile tarafı için ek bir paging requirement yok.

---

## 5. Authentication Özeti

JWT ayarları `appsettings.json` içinde:

```json
"Jwt": {
  "Issuer": "University.Api",
  "Audience": "University.Client",
  "Key": "buraya-uzun-bir-secret-key"
}
```

Program.cs tarafında:

```csharp
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
```

ve `AddAuthentication().AddJwtBearer(...)` ile doğrulama yapılıyor.

---

## 6. API Gateway (University.Gateway)

**Proje:** `University.Gateway`  
**Local adres (örnek):** `http://localhost:5207`

### 6.1 Reverse Proxy

`Program.cs` içinde:

- `AddHttpClient("backend", ...)` ile backend base URL:

```csharp
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new Uri("http://localhost:5099"); // University.Api
});
```

- Tüm istekler:

```csharp
app.Map("/{**catch-all}", async context => { ... });
```

ile yakalanıp `University.Api`’ye forward ediliyor.

- Gönderilenler:
  - HTTP method (GET/POST/...)
  - Path + Query
  - Body (POST/PUT vs) → `StreamContent`
  - Header’lar (Authorization dahil)

Backend’den gelen:

- Status code
- Header’lar
- Body

aynı şekilde client’a geri yazılıyor.

---

### 6.2 Rate Limiting (Mobile Query Tuition)

Requirements: 

> "Limit Mobile App Query Tuition call to 3 times per student per day."

Bu, **Gateway** içinde middleware olarak implement edildi.

Mantık:

- Sadece
  ```text
  GET /api/v1/mobile/tuition?studentNo=...
  ```
  isteklerini yakalar.
- `studentNo` boş değilse:
  - Key:  
    ```text
    yyyyMMdd:studentNo
    ```
    örn: `20251130:S123`
  - `ConcurrentDictionary<string,int>` içinde sayaç tutulur.
  - Sayaç > 3 ise:
    - `StatusCode = 429 TooManyRequests`
    - Body: `"Daily limit exceeded (3 per student)"`
    - İstek **backend’e gitmez**.

Gateway terminalinde bu durumda log satırı gözükür.  
Aynı öğrenci için 4. istekte bu limit devreye girer.

---

### 6.3 Logging (Gateway)

Requirements’teki request/response logging alanları **Gateway** seviyesinde tutuluyor.

Gateway tarafındaki log formatı örneği:

```text
GW GET /api/v1/mobile/tuition?studentNo=S123 Status=200 Req=0 Res=123 Latency=5ms IP=::1 Time=2025-11-30T10:51:30.218Z Headers=...
```

**Request-level log içeriği:**

- HTTP method (GET/POST/PUT/DELETE)
- Full request path + query (`/api/v1/...?...`)
- Request timestamp (UTC, ISO string)
- Source IP (`context.Connection.RemoteIpAddress`)
- Headers (string olarak join’lenmiş)
- Request size (`Request.ContentLength` → byte)
- Auth durumu:
  - Header’da `Authorization` varsa → `HasAuthHeader`
  - Yoksa → `NoAuthHeader`  
  (Authentication gerçek anlamda backend’de, gateway sadece header var mı yok mu logluyor.)

**Response-level log içeriği:**

- Status code (200, 400, 401, 403, 429, 500, …)
- Response latency (ms, `Stopwatch`)
- Response size (`Response.ContentLength`)

Mapping template kullanılmadığı için "mapping template failures" için ayrıca bir alan tutulmadı; pratikte her cevap normal template’siz olduğu için "yok (false)" varsayılıyor. README’de bu not olarak belirtiliyor.

---

## 7. Swagger & Gateway URL

Requirements:

> "All APIs must have Swagger UI or document. Swagger should point to the API Gateway invoke URL"

Swagger sadece **University.Api** projesinde var.  
`Program.cs`’te Swagger konfigürasyonu:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "University Tuition API",
        Version = "v1"
    });

    // Swagger'daki "Servers" kısmı → Gateway URL
    options.AddServer(new OpenApiServer
    {
        Url = "http://localhost:5207",
        Description = "University API Gateway"
    });

    // JWT Bearer tanımı vs.
});
```

Bu sayede:

- Swagger UI URL’i:
  - `http://localhost:5099/swagger`
- Ama Swagger’ın "Servers" bölümünde:
  - `http://localhost:5207` (Gateway) gösteriliyor.
- Böylece Swagger’dan butonlara bastığın her şey aslında **Gateway üzerinden** backend’e gidiyor.

---

## 8. DB Yapılandırması (InMemory & Azure SQL)

`University.Api/Program.cs` içindeki DB konfig:

```csharp
#if DEBUG
    // Local geliştirme: InMemory DB
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("TuitionDb"));
#else
    // Publish / Production: Azure SQL
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
#endif
```

- **DEBUG** build’de:
  - `dotnet run` ile çalıştırınca EF Core **InMemoryDatabase("TuitionDb")** kullanır.
  - Tablolar bu memory DB içinde oluşur.
- **RELEASE / Publish** build’de:
  - `DefaultConnection` connection string’i üzerinden **Azure SQL** kullanılır.

Azure tarafı için:

- Azure Portal’da bir SQL Database oluşturulur.
- Connection string alınır, `appsettings.Production.json` veya Azure App Service konfigurasyonuna eklenir.
- Publish edildiğinde API doğrudan Azure SQL’e bağlanır.

---

## 9. Local Çalıştırma Adımları

### 9.1 Önkoşullar

- .NET 8 SDK
- Repo indirildi:  
  `C:\Users\...\UniversityTuitionSystem\`

### 9.2 Adımlar

1. **API’yi çalıştır (University.Api)**

   ```bash
   cd University.Api
   dotnet run
   ```

   Konsolda örneğin:

   ```text
   Now listening on: http://localhost:5099
   Now listening on: https://localhost:7xxx
   ```

2. **Gateway’i çalıştır (University.Gateway)**

   Ayrı bir terminal:

   ```bash
   cd University.Gateway
   dotnet run
   ```

   Konsolda örneğin:

   ```text
   Now listening on: http://localhost:5207
   ```

3. **Swagger’a git**

   - Tarayıcıda: `http://localhost:5099/swagger`
   - "Servers" kısmında `http://localhost:5207` görünür.
   - Artık swagger’dan yapılan çağrılar gateway üzerinden akar.

4. **Örnek Senaryo**

   1. `POST /api/v1/auth/login` ile token al.
   2. Swagger’daki **Authorize** butonuna tıklayıp  
      `Bearer <token>` formatında token gir.
   3. `POST /api/v1/admin/tuition` ile
      ```json
      {
        "studentNo": "S123",
        "term": "2024-FALL",
        "totalAmount": 1000
      }
      ```
      gönder ve tuition ekle.
   4. `GET /api/v1/banking/tuition?studentNo=S123` ile borç sorgula.
   5. `POST /api/v1/banking/pay` ile ödeme yap:
      ```json
      {
        "studentNo": "S123",
        "term": "2024-FALL",
        "amount": 500
      }
      ```
   6. `GET /api/v1/mobile/tuition?studentNo=S123` ile mobile üzerinden borcu sorgula.
   7. Aynı mobile endpoint’i **gateway URL’i** üzerinden 4 kez çağır:
      - URL: `http://localhost:5207/api/v1/mobile/tuition?studentNo=S123`
      - İlk 3 çağrı → JSON response
      - 4. çağrı → `429` + `"Daily limit exceeded (3 per student)"`

   8. Gateway terminalinde log satırlarını incele (`GW GET ... Status=...`).

---

## 10. Cloud Hosting (Ödev Gereksinimi İçin Not)

Requirements’e göre:

- API’lerin bir cloud provider üzerinde host edilmesi bekleniyor (Azure, Render, vb.).
- Kod tarafı buna hazır (Azure SQL + configurable base URL).
- Publish adımları sırasında:
  - `University.Api` → App Service (ör: `https://<api>.azurewebsites.net`)
  - `University.Gateway` → App Service (ör: `https://<gateway>.azurewebsites.net`)
  - Gateway’in `BaseAddress`’ini API’nin cloud URL’ine göre güncellemek gerekiyor.
  - Swagger `AddServer` URL’i de cloud’daki gateway URL’ine göre güncellenebilir.

---

## 11. Varsayımlar ve Basitleştirmeler

- Kimlik doğrulama için **tek bir sabit admin kullanıcısı** (username = `admin`, password = `password`) yeterli görüldü.
- Mapping template mekanizması kullanılmadığı için, bu alandaki failure takibi yapılmıyor; log’larda dolaylı olarak "yok" kabul ediliyor.
- Banking `Pay` endpoint’i requirement tablosuna göre auth olmadan açık bırakıldı.
- Mobile tarafında sadece toplam `TuitionTotal` ve `Balance` döndürülüyor; satır satır dönem detayları gerekmiyor.

Bu README, projede yazılan kodlarla birebir uyumlu olacak şekilde hazırlanmıştır.
