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
