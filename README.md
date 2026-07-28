# Notification Connector Project

Bu proje, farklı protokoller üzerinden gelen bildirim mesajlarını ortak bir formata dönüştüren ve canlı olarak listeleyen dockerize bir bildirim connector sistemidir.

Proje kapsamında Webhook, WebSocket, RabbitMQ ve Redis kaynaklarından gelen mesajlar connector tarafından alınır, normalize edilir, backend'e gönderilir ve frontend üzerinde görüntülenir.

Repository:

```txt
https://github.com/anilalper4/notification-connector-project
```

---

## Proje Amacı

Bu projenin amacı, farklı protokollerden gelen bildirim mesajlarını protokolden bağımsız bir connector yapısı ile tek bir ortak formata dönüştürmektir.

Temel hedefler:

- Farklı kaynaklardan mesaj alabilen adapter yapısı kurmak.
- Webhook, WebSocket, RabbitMQ ve Redis kaynaklarını desteklemek.
- Gelen ham mesajları ortak `NotificationEnvelope` formatına normalize etmek.
- Backend tarafında mesajları saklamak ve listelemek.
- Aynı mesajların tekrar kaydedilmesini engellemek.
- Frontend üzerinde bildirimleri canlı olarak göstermek.
- Tüm sistemi Docker Compose ile tek komutla çalıştırmak.
- Backend geçici olarak erişilemez olduğunda mesaj kaybını azaltmak için retry/outbox mantığı eklemek.

---

## Genel Mimari

Sistem temel olarak şu parçalardan oluşur:

```txt
Simulator
    ↓
Webhook / WebSocket / RabbitMQ / Redis
    ↓
Connector
    ↓
Backend
    ↓
Frontend
```

Detaylı akış:

```txt
Simulator
    ↓
Protocol Sources
    ↓
Connector Adapters
    ↓
Connector Core
    ↓
Notification Normalizer
    ↓
Notification Outbox
    ↓
Backend Delivery Worker
    ↓
Backend API
    ↓
Frontend Notification List
```

---

## Servisler

### 1. Simulator

Simulator, test amacıyla farklı protokoller üzerinden bildirim mesajları üretir.

Desteklenen üretim kanalları:

- Webhook
- WebSocket
- RabbitMQ
- Redis

Simulator belirli aralıklarla örnek mesajlar üretir ve bu mesajları farklı kaynaklardan connector sistemine gönderir.

Örnek source değerleri:

```txt
simulator-webhook
simulator-websocket
simulator-rabbitmq
simulator-redis
```

---

### 2. Connector

Connector, projenin ana parçasıdır.

Görevleri:

- Farklı protokoller için adapter yapısını yönetmek.
- Aktif kaynakları register etmek.
- Gerekirse kaynakları unregister etmek.
- Ham mesajları almak.
- Mesajları ortak formata normalize etmek.
- Backend'e gönderilecek mesajları outbox kuyruğuna eklemek.
- Backend'e gönderim başarısız olursa tekrar denemek.

Connector içinde kullanılan temel yapılar:

- `ISourceAdapter`
- `IConnector`
- `NotificationConnector`
- `NotificationNormalizer`
- `WebhookSourceAdapter`
- `WebSocketSourceAdapter`
- `RabbitMqSourceAdapter`
- `RedisSourceAdapter`
- `NotificationOutbox`
- `BackendDeliveryWorker`

---

### 3. Backend

Backend, ASP.NET Core Minimal API olarak geliştirilmiştir.

Görevleri:

- Connector'dan gelen normalize edilmiş bildirimleri almak.
- Bildirimleri bellekte saklamak.
- `deduplicationKey` değerine göre tekrar eden mesajları engellemek.
- Frontend için bildirim listesini API üzerinden sunmak.

Backend endpointleri:

```txt
GET  /
GET  /api/notifications
POST /api/notifications
```

---

### 4. Frontend

Frontend, React + TypeScript + Vite ile geliştirilmiştir.

Görevleri:

- Backend API'den bildirimleri almak.
- Bildirimleri canlı liste halinde göstermek.
- Belirli aralıklarla backend'den güncel verileri çekmek.

Frontend adresi:

```txt
http://localhost:3000
```

---

### 5. RabbitMQ

RabbitMQ, mesaj kuyruğu tabanlı bildirim kaynağı olarak kullanılır.

RabbitMQ Management Panel:

```txt
http://localhost:15672
```

Giriş bilgileri:

```txt
guest / guest
```

Kuyruk adı:

```txt
notifications.rabbitmq
```

RabbitMQ bağlantı adresi:

```txt
amqp://guest:guest@rabbitmq:5672
```

---

### 6. Redis

Redis, Pub/Sub channel üzerinden bildirim kaynağı olarak kullanılır.

Redis bağlantı adresi:

```txt
redis:6379
```

Redis channel adı:

```txt
notifications.redis
```

---

## Ortak Bildirim Formatı

Connector, farklı kaynaklardan gelen mesajları ortak `NotificationEnvelope` formatına dönüştürür.

Örnek format:

```json
{
  "source": "simulator-webhook",
  "type": "order.created",
  "message": "Webhook notification #1 - order.created",
  "occurredAt": "2026-07-28T07:54:55.0853994+00:00",
  "deduplicationKey": "simulator-webhook-1"
}
```

Alanlar:

- `source`: Mesajın geldiği kaynak.
- `type`: Mesaj tipi.
- `message`: Bildirim içeriği.
- `occurredAt`: Mesajın oluşma zamanı.
- `deduplicationKey`: Tekilleştirme için kullanılan anahtar.

---

## Kaynak Seçimi

Connector'ın hangi kaynakları dinleyeceği environment variable üzerinden belirlenir.

Varsayılan değer:

```txt
CONNECTOR_SOURCES=webhook,websocket,rabbitmq,redis
```

Örnek olarak yalnızca Webhook ve Redis kaynaklarını dinlemek için:

```txt
CONNECTOR_SOURCES=webhook,redis
```

Bu yapı sayesinde kaynaklar kod değiştirmeden açılıp kapatılabilir.

---

## Docker ile Çalıştırma

Projeyi çalıştırmak için ana dizinde şu komut kullanılır:

```bash
docker compose up --build
```

Bu komut şu servisleri ayağa kaldırır:

- RabbitMQ
- Redis
- Backend
- Connector
- Simulator
- Frontend

Sistemi kapatmak için:

```bash
docker compose down
```

---

## Servis Adresleri

Frontend:

```txt
http://localhost:3000
```

Backend API:

```txt
http://localhost:8080/api/notifications
```

Connector:

```txt
http://localhost:8090
```

Simulator:

```txt
http://localhost:7001
```

RabbitMQ Management Panel:

```txt
http://localhost:15672
```

RabbitMQ giriş bilgileri:

```txt
guest / guest
```

---

## Environment Variables

Connector için kullanılan temel environment variable değerleri:

```txt
PORT=8090
BACKEND_URL=http://backend:8080
CONNECTOR_SOURCES=webhook,websocket,rabbitmq,redis
WEBSOCKET_SOURCE_URL=ws://simulator:7001/ws
RABBITMQ_URI=amqp://guest:guest@rabbitmq:5672
RABBITMQ_QUEUE=notifications.rabbitmq
REDIS_CONNECTION_STRING=redis:6379
REDIS_CHANNEL=notifications.redis
BACKEND_RETRY_DELAY_SECONDS=3
SHUTDOWN_FLUSH_SECONDS=10
```

Simulator için kullanılan temel environment variable değerleri:

```txt
PORT=7001
CONNECTOR_WEBHOOK_URL=http://connector:8090/webhook
RABBITMQ_URI=amqp://guest:guest@rabbitmq:5672
RABBITMQ_QUEUE=notifications.rabbitmq
REDIS_CONNECTION_STRING=redis:6379
REDIS_CHANNEL=notifications.redis
```

---

## Hafta 1 Kapsamı

Birinci hafta kapsamında projenin temel uçtan uca akışı kurulmuştur.

Yapılanlar:

- Backend projesi oluşturuldu.
- Frontend projesi oluşturuldu.
- Simulator projesi oluşturuldu.
- Backend üzerinde bildirim alma ve listeleme endpointleri eklendi.
- Frontend üzerinde bildirim listesi oluşturuldu.
- Simulator üzerinden test mesajları üretildi.
- Dockerfile dosyaları oluşturuldu.
- Docker Compose ile backend, frontend ve simulator birlikte çalıştırıldı.

Birinci hafta sonunda temel akış:

```txt
Simulator
    ↓
Backend
    ↓
Frontend
```

---

## Hafta 2 Kapsamı

İkinci hafta kapsamında connector çekirdeği ve ilk adapter yapıları geliştirildi.

Yapılanlar:

- Connector projesi oluşturuldu.
- `ISourceAdapter` interface'i eklendi.
- `IConnector` interface'i eklendi.
- `NotificationConnector` çekirdeği geliştirildi.
- `NotificationNormalizer` eklendi.
- Webhook adapter geliştirildi.
- WebSocket adapter geliştirildi.
- Connector'ın backend'e mesaj göndermesi sağlandı.
- Simulator, Webhook ve WebSocket üzerinden mesaj üretecek şekilde güncellendi.
- Docker Compose dosyasına connector servisi eklendi.

İkinci hafta sonunda çalışan akış:

```txt
Simulator Webhook Sender
    ↓
Connector Webhook Adapter
    ↓
Connector Core
    ↓
Backend
    ↓
Frontend

Simulator WebSocket Server
    ↓
Connector WebSocket Adapter
    ↓
Connector Core
    ↓
Backend
    ↓
Frontend
```

---

## Hafta 3 Kapsamı

Üçüncü hafta kapsamında RabbitMQ ve Redis kaynakları sisteme dahil edilmiştir.

Yapılanlar:

- RabbitMQ adapter geliştirildi.
- Redis adapter geliştirildi.
- Connector tarafında RabbitMQ ve Redis kaynaklarının register edilmesi sağlandı.
- Simulator, RabbitMQ kuyruğuna mesaj basacak şekilde güncellendi.
- Simulator, Redis channel üzerinden mesaj yayınlayacak şekilde güncellendi.
- Docker Compose dosyasına RabbitMQ container'ı eklendi.
- Docker Compose dosyasına Redis container'ı eklendi.
- Connector kaynak seçimi environment variable üzerinden yönetilecek şekilde güncellendi.
- Sistem dört farklı kaynakla uçtan uca çalışacak hale getirildi.

Üçüncü hafta sonunda çalışan akış:

```txt
Simulator Webhook Sender
        ↓
Connector Webhook Adapter
        ↓
Connector Core
        ↓
Backend
        ↓
Frontend

Simulator WebSocket Server
        ↓
Connector WebSocket Adapter
        ↓
Connector Core
        ↓
Backend
        ↓
Frontend

Simulator RabbitMQ Publisher
        ↓
RabbitMQ Queue
        ↓
Connector RabbitMQ Adapter
        ↓
Connector Core
        ↓
Backend
        ↓
Frontend

Simulator Redis Publisher
        ↓
Redis Pub/Sub Channel
        ↓
Connector Redis Adapter
        ↓
Connector Core
        ↓
Backend
        ↓
Frontend
```

---

## Hafta 4 Kapsamı

Dördüncü hafta kapsamında sistemin dayanıklılık, hata toleransı ve final testleri üzerinde çalışılmıştır.

Yapılanlar:

- Connector tarafına memory tabanlı outbox yapısı eklendi.
- Backend'e gönderilecek mesajların önce outbox kuyruğuna alınması sağlandı.
- Backend erişilemez olduğunda mesajların kaybolmaması ve tekrar denenmesi sağlandı.
- Backend tekrar erişilebilir olduğunda bekleyen mesajların gönderilmeye devam etmesi sağlandı.
- RabbitMQ adapter tarafında otomatik recovery ayarları güçlendirildi.
- Redis adapter tarafında retry/reconnect ayarları eklendi.
- WebSocket bağlantısı koptuğunda tekrar bağlanmayı deneyecek yapı korundu.
- Connector kapanırken eldeki mesajları göndermeye çalışacak shutdown flush mantığı eklendi.
- Docker Compose üzerinde retry ve shutdown süreleri environment variable ile yönetilecek hale getirildi.
- Final test senaryoları `TEST_SCENARIOS.md` dosyasında belgelendi.

---

## Dayanıklılık ve Hata Toleransı

Connector, backend'e doğrudan mesaj göndermek yerine mesajları önce outbox kuyruğuna alır.

Akış:

```txt
Source Adapter
    ↓
Connector Core
    ↓
NotificationOutbox
    ↓
BackendDeliveryWorker
    ↓
Backend
```

Backend erişilemezse:

1. Mesaj backend'e gönderilemez.
2. Mesaj outbox kuyruğuna geri alınır.
3. Belirlenen süre kadar beklenir.
4. Gönderim tekrar denenir.
5. Backend tekrar erişilebilir olduğunda mesaj gönderimi devam eder.

İlgili environment variable değerleri:

```txt
BACKEND_RETRY_DELAY_SECONDS=3
SHUTDOWN_FLUSH_SECONDS=10
```

---

## Final Test Özeti

Final testlerde şu senaryolar doğrulanmıştır:

- Sistem `docker compose up --build` komutuyla ayağa kaldırıldı.
- Frontend üzerinde dört farklı kaynaktan mesaj geldiği doğrulandı:
  - `simulator-webhook`
  - `simulator-websocket`
  - `simulator-rabbitmq`
  - `simulator-redis`
- Backend API üzerinde dört farklı kaynaktan gelen mesajlar görüntülendi.
- RabbitMQ panelinde queue ve consumer bilgileri görüntülendi.
- Backend geçici olarak durdurulduğunda connector'ın mesajları retry ettiği doğrulandı.
- Bozuk mesaj gönderildiğinde connector'ın kapanmadığı ve sistemin çalışmaya devam ettiği doğrulandı.
- Simulator durdurulduğunda WebSocket bağlantısının başarısız olduğu ve connector'ın tekrar bağlanmayı denediği loglarda gözlemlendi.
- Simulator tekrar başlatıldığında mesaj akışının devam ettiği doğrulandı.

Detaylı test senaryoları:

```txt
TEST_SCENARIOS.md
```

---

## Test Senaryoları

Temel test dosyası:

```txt
TEST_SCENARIOS.md
```

Bu dosyada şu testler açıklanmıştır:

- Tüm sistemi ayağa kaldırma
- Dört kaynaktan mesaj alma
- RabbitMQ kontrolü
- Redis kontrolü
- Config ile kaynak seçimi
- Backend erişilemez olduğunda retry/outbox testi
- Bozuk mesaj toleransı
- Tekilleştirme kontrolü
- WebSocket reconnect gözlemi
- Sistemi kapatma

---

## Tekilleştirme

Backend tarafında `deduplicationKey` kullanılarak aynı mesajın tekrar tekrar eklenmesi engellenir.

Aynı `deduplicationKey` değerine sahip bir mesaj tekrar geldiğinde backend bu mesajı yeni kayıt olarak eklemez.

Bu sayede simulator veya kaynak sistem aynı mesajı tekrar gönderse bile frontend üzerinde gereksiz tekrarların oluşması engellenir.

---

## Bozuk Mesaj Toleransı

Connector, beklenen formata tam uymayan veya bozuk payload içeren mesajlarda sistemi kapatmadan çalışmaya devam eder.

Bozuk mesaj gönderimi sırasında doğrulananlar:

- Connector kapanmadı.
- Frontend çalışmaya devam etti.
- Backend çalışmaya devam etti.
- Sonraki geçerli mesajlar işlenmeye devam etti.

---

## WebSocket Reconnect

WebSocket kaynağına erişilemediğinde connector bağlantı hatasını loglar ve belirli aralıklarla yeniden bağlanmayı dener.

Test sırasında simulator geçici olarak durdurulmuş ve connector loglarında şu davranış gözlemlenmiştir:

```txt
Connecting to WebSocket source: ws://simulator:7001/ws
WebSocket connection failed. Retrying in 3 seconds.
```

Simulator tekrar başlatıldığında mesaj akışının devam ettiği doğrulanmıştır.

---

## RabbitMQ Kontrolü

RabbitMQ Management Panel üzerinden queue ve consumer durumları kontrol edilebilir.

Beklenen durum:

- `notifications.rabbitmq` kuyruğu oluşur.
- Queue üzerinde consumer görünür.
- Mesajlar connector tarafından hızlıca tüketildiği için queue çoğunlukla 0 görünür.
- Publish ve consumer ack oranları hareket eder.

Queue'nun 0 görünmesi hata değildir. Bu, connector'ın mesajları hızlı tükettiğini gösterir.

---

## Redis Kontrolü

Redis Pub/Sub channel üzerinden gelen mesajlar connector tarafından alınır.

Beklenen durum:

- Simulator Redis channel üzerine mesaj yayınlar.
- Connector Redis adapter mesajları alır.
- Backend API içinde `simulator-redis` source değerine sahip mesajlar görünür.
- Frontend üzerinde `simulator-redis` etiketiyle bildirimler listelenir.

---

## Klasör Yapısı

```txt
notification-connector-project/
│
├── backend/
│   ├── Dockerfile
│   └── ...
│
├── connector/
│   ├── Adapters/
│   ├── Contracts/
│   ├── Core/
│   ├── Models/
│   ├── Services/
│   ├── Dockerfile
│   └── Program.cs
│
├── frontend/
│   ├── src/
│   ├── Dockerfile
│   └── ...
│
├── simulator/
│   ├── Dockerfile
│   └── Program.cs
│
├── docker-compose.yml
├── README.md
└── TEST_SCENARIOS.md
```

---

## Kullanılan Teknolojiler

- .NET
- ASP.NET Core Minimal API
- React
- TypeScript
- Vite
- Docker
- Docker Compose
- RabbitMQ
- Redis
- WebSocket
- Webhook
- Git
- GitHub

---

## Final Durum

Proje sonunda sistem dört farklı bildirim kaynağını destekleyecek hale getirilmiştir:

- Webhook
- WebSocket
- RabbitMQ
- Redis

Simulator bu kaynaklar üzerinden test mesajları üretir. Connector bu mesajları adapter yapısı üzerinden alır, ortak formata normalize eder ve backend'e iletir. Backend mesajları `deduplicationKey` değerine göre tekilleştirerek saklar. Frontend ise backend API üzerinden gelen bildirimleri canlı olarak listeler.

Final durumda sistem tek komutla ayağa kaldırılabilir:

```bash
docker compose up --build
```

Sistem şu özellikleri destekler:

- Çoklu protokol desteği
- Adapter tabanlı connector mimarisi
- Ortak bildirim formatı
- Backend API
- Frontend canlı listeleme
- RabbitMQ desteği
- Redis desteği
- WebSocket desteği
- Webhook desteği
- Deduplication
- Retry/outbox mantığı
- Bozuk mesaj toleransı
- WebSocket reconnect gözlemi
- Docker Compose ile tek komut çalıştırma
