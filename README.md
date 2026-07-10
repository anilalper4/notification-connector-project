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
