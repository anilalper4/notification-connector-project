# Protocol Independent Notification Connector

Bu proje, farklı protokollerden gelen mesajları ortak bir bildirim formatına dönüştürüp web arayüzünde canlı olarak listelemeyi amaçlayan 4 haftalık staj projesidir.

## 1. Hafta Kapsamı

İlk hafta kapsamında uçtan uca çalışan basit bir ince hat kurulmuştur.

Mevcut akış:

```txt
Simulator
  ↓ HTTP POST
Backend
  ↓ HTTP GET
Frontend
```

## 2. Hafta Kapsamı

İkinci hafta kapsamında connector çekirdeği ve iki farklı adapter geliştirildi:

- Connector core yapısı oluşturuldu.
- `IConnector` ve `ISourceAdapter` sözleşmeleri eklendi.
- Kaynakların `Register` / `Unregister` mantığıyla connector çekirdeğine bağlanması sağlandı.
- Webhook adapter geliştirildi.
- WebSocket adapter geliştirildi.
- Gelen ham mesajların ortak `NotificationEnvelope` formatına normalize edilmesi sağlandı.
- Connector'ın normalize edilen mesajları backend'e göndermesi sağlandı.
- Simulator Webhook ve WebSocket kaynakları üretecek şekilde güncellendi.
- Docker Compose akışına connector servisi eklendi.

2. hafta sonunda çalışan akış:

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

## 3. Hafta Kapsamı

Üçüncü hafta kapsamında RabbitMQ ve Redis kaynakları sisteme dahil edilmiştir.

Bu hafta yapılanlar:

- RabbitMQ adapter geliştirildi.
- Redis adapter geliştirildi.
- Connector tarafında RabbitMQ ve Redis kaynaklarının `Register` / `Unregister` mantığıyla bağlanması sağlandı.
- Simulator, RabbitMQ kuyruğuna mesaj basacak şekilde güncellendi.
- Simulator, Redis channel üzerinden mesaj yayınlayacak şekilde güncellendi.
- Docker Compose dosyasına RabbitMQ container'ı eklendi.
- Docker Compose dosyasına Redis container'ı eklendi.
- Connector kaynak seçimi environment variable üzerinden yönetilecek şekilde güncellendi.
- Sistem dört farklı kaynakla uçtan uca çalışacak hale getirildi.

3. hafta sonunda çalışan akış:

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
