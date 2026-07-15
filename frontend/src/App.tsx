import { useEffect, useState } from "react";
import "./App.css";

type NotificationItem = {
  id: string;
  source: string;
  type: string;
  message: string;
  occurredAt: string;
  receivedAt: string;
  deduplicationKey: string;
};

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8080";

function App() {
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [status, setStatus] = useState("Henüz veri çekilmedi.");

  async function loadNotifications() {
    try {
      const response = await fetch(`${API_BASE_URL}/api/notifications`);

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const data = (await response.json()) as NotificationItem[];

      setNotifications(data);
      setStatus(`Son güncelleme: ${new Date().toLocaleTimeString("tr-TR")}`);
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Bilinmeyen hata";
      setStatus(`Backend'e bağlanılamadı: ${message}`);
    }
  }

  useEffect(() => {
    const initialLoadId = window.setTimeout(() => {
      void loadNotifications();
    }, 0);

    const intervalId = window.setInterval(() => {
      void loadNotifications();
    }, 2000);

    return () => {
      window.clearTimeout(initialLoadId);
      window.clearInterval(intervalId);
    };
  }, []);

  return (
    <main className="page">
      <section className="hero">
        <div>
          <p className="eyebrow">2. Hafta Connector Akışı</p>
          <h1>Canlı Bildirim Listesi</h1>
          <p className="description">
            Simulator tarafından Webhook ve WebSocket kaynaklarından üretilen
            mesajlar connector üzerinden normalize edilir, backend'e gönderilir
            ve bu ekranda listelenir.
          </p>
        </div>

        <button onClick={loadNotifications}>Manuel Yenile</button>
      </section>

      <section className="status-card">
        <strong>Durum:</strong> {status}
      </section>

      <section className="list">
        {notifications.length === 0 ? (
          <div className="empty-state">
            Henüz bildirim yok. Backend ve simulator çalışıyorsa birkaç saniye
            içinde burada mesaj görünecek.
          </div>
        ) : (
          notifications.map((notification) => (
            <article className="notification-card" key={notification.id}>
              <div className="card-header">
                <span className="badge">{notification.source}</span>
                <span className="type">{notification.type}</span>
              </div>

              <p className="message">{notification.message}</p>

              <div className="meta">
                <span>
                  Oluşma:{" "}
                  {new Date(notification.occurredAt).toLocaleString("tr-TR")}
                </span>
                <span>
                  Alınma:{" "}
                  {new Date(notification.receivedAt).toLocaleString("tr-TR")}
                </span>
              </div>

              <code>{notification.deduplicationKey}</code>
            </article>
          ))
        )}
      </section>
    </main>
  );
}

export default App;
