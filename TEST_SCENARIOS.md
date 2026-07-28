@'

# Test Scenarios

Bu dosya, proje teslimi öncesinde çalıştırılan temel senaryo testlerini içerir.

## 1. Tüm sistemi ayağa kaldırma

Komut:

```bash
docker compose up --build
```

Beklenen sonuç:

- RabbitMQ container çalışır.
- Redis container çalışır.
- Backend çalışır.
- Connector çalışır.
- Simulator çalışır.
- Frontend çalışır.

Frontend:

```txt
http://localhost:3000
```

Backend API:

```txt
http://localhost:8080/api/notifications
```

RabbitMQ Management Panel:

```txt
http://localhost:15672
```

RabbitMQ giriş bilgileri:

```txt
guest / guest
```

## 2. Dört kaynaktan mesaj alma

Amaç:

Simulator tarafından dört farklı kaynaktan üretilen mesajların connector üzerinden backend'e ulaştığını ve frontend üzerinde listelendiğini doğrulamak.

Beklenen frontend source değerleri:

```txt
simulator-webhook
simulator-websocket
simulator-rabbitmq
simulator-redis
```

Beklenen sonuç:

- Frontend üzerinde dört farklı kaynağın tamamı görüntülenir.
- Backend API üzerinden de aynı kaynaklardan gelen mesajlar görülebilir.

Backend API kontrol adresi:

```txt
http://localhost:8080/api/notifications
```

## 3. RabbitMQ kontrolü

Amaç:

RabbitMQ kuyruğuna mesaj basıldığını ve connector tarafından tüketildiğini doğrulamak.

RabbitMQ Management Panel:

```txt
http://localhost:15672
```

Giriş:

```txt
guest / guest
```

Beklenen durum:

- `notifications.rabbitmq` kuyruğu oluşur.
- Queue üzerinde consumer görünür.
- Mesajlar connector tarafından tüketildiği için queue çoğunlukla 0 görünür.
- Publish ve consumer ack oranları hareket eder.

Not:

Queue'nun 0 görünmesi hata değildir. Mesajlar connector tarafından hızlıca tüketildiği için kuyrukta beklemeden işlenebilir.

## 4. Redis kontrolü

Amaç:

Redis Pub/Sub channel üzerinden yayınlanan mesajların connector tarafından alındığını doğrulamak.

Beklenen durum:

- Simulator Redis channel üzerine mesaj yayınlar.
- Connector Redis adapter bu mesajları alır.
- Backend API içinde `simulator-redis` source değerine sahip mesajlar görünür.
- Frontend üzerinde `simulator-redis` etiketiyle bildirimler listelenir.

Redis channel adı:

```txt
notifications.redis
```

## 5. Config ile kaynak seçimi

Amaç:

Connector'ın dinleyeceği kaynakların environment variable üzerinden değiştirilebildiğini göstermek.

`docker-compose.yml` içinde connector servisi altında şu değer bulunur:

```txt
CONNECTOR_SOURCES=webhook,websocket,rabbitmq,redis
```

Örnek olarak yalnızca Webhook ve Redis kaynaklarını dinlemek için bu değer şöyle değiştirilebilir:

```txt
CONNECTOR_SOURCES=webhook,redis
```

Beklenen sonuç:

- Connector sadece belirtilen kaynakları register eder.
- Image yeniden build edilmeden environment variable üzerinden kaynak seçimi yapılabilir.
- Devre dışı bırakılan kaynaklardan gelen mesajlar backend'e iletilmez.

Not:

Bu test için `docker-compose.yml` geçici olarak değiştirilip sistem yeniden başlatılabilir. Testten sonra değer tekrar şu hale alınmalıdır:

```txt
CONNECTOR_SOURCES=webhook,websocket,rabbitmq,redis
```

## 6. Backend geçici olarak erişilemezse

Amaç:

Connector'ın backend'e ulaşamadığında mesajları hemen kaybetmemesini ve retry/outbox mekanizmasının çalıştığını doğrulamak.

Senaryo:

1. Sistem çalışırken yeni bir terminalde backend container durdurulur:

```bash
docker compose stop backend
```

2. Simulator mesaj üretmeye devam eder.

3. Connector loglarında backend'e gönderimin başarısız olduğu ve mesajların tekrar deneneceği görülür.

Beklenen log örnekleri:

```txt
Backend is not reachable. Notification will be retried.
Notification requeued. Pending outbox count: ...
```

4. Backend tekrar başlatılır:

```bash
docker compose start backend
```

5. Connector bekleyen mesajları backend'e göndermeye devam eder.

6. Backend API tekrar kontrol edilir:

```txt
http://localhost:8080/api/notifications
```

Beklenen sonuç:

- Backend kapalıyken connector mesajları kaybetmez.
- Mesajlar outbox kuyruğuna geri alınır.
- Backend tekrar açıldığında gönderim devam eder.

## 7. Bozuk mesaj toleransı

Amaç:

Bozuk veya beklenen formata tam uymayan mesajların sistemi düşürmediğini doğrulamak.

Webhook endpointine manuel bozuk mesaj gönderilebilir.

PowerShell komutu:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8090/webhook `
  -Method Post `
  -ContentType "application/json" `
  -Body 'invalid-json'
```

Beklenen sonuç:

- Connector kapanmaz.
- Sistem çalışmaya devam eder.
- Bozuk mesaj normalize edilerek `invalid.message` tipiyle backend'e iletilebilir veya loglanır.
- Sonraki geçerli mesajlar işlenmeye devam eder.

## 8. Tekilleştirme kontrolü

Amaç:

Aynı `deduplicationKey` değerine sahip mesajların backend tarafında tekrar tekrar eklenmediğini doğrulamak.

Aynı mesaj iki kez gönderilebilir:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8090/webhook `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"source":"manual-test","type":"duplicate.test","message":"Duplicate test message","deduplicationKey":"duplicate-test-1"}'
```

Aynı komut ikinci kez tekrar çalıştırılır.

Beklenen sonuç:

- Backend aynı `deduplicationKey` değerine sahip mesajı ikinci kez yeni kayıt olarak eklemez.
- Backend duplicate mesajı yok sayar veya mevcut kayıtla cevap döner.

## 9. WebSocket reconnect gözlemi

Amaç:

Connector'ın WebSocket kaynağına bağlanamadığında tekrar bağlanmayı denediğini doğrulamak.

Senaryo:

1. Sistem başlatılır.

2. Simulator geçici olarak durdurulabilir:

```bash
docker compose stop simulator
```

3. Connector loglarında WebSocket bağlantısının koptuğu ve tekrar deneneceği görülür.

4. Simulator tekrar başlatılır:

```bash
docker compose start simulator
```

Beklenen sonuç:

- Connector WebSocket kaynağına tekrar bağlanmayı dener.
- Simulator tekrar açıldığında WebSocket mesajları yeniden frontend/backend tarafına ulaşır.

## 10. Sistemi kapatma

Komut:

```bash
docker compose down
```

Beklenen sonuç:

- Container'lar durur.
- Compose network temizlenir.
- Sistem kontrollü şekilde kapanır.
