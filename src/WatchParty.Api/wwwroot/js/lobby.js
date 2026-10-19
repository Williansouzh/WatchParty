// Background connection while browsing: hears when friends create or start a room and shows an alert
// (a toast, plus a system notification if the tab is in the background and the user allowed it).
import { toast } from './ui.js';

let connection = null;

export async function startLobby(user) {
  if (connection) return;
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/room')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Error)
    .build();

  connection.on('RoomActivity', (activity) => {
    if (activity.byUserId === user.id) return;
    if (location.hash.toUpperCase().includes(`/ROOM/${activity.code}`)) return;

    const text = activity.kind === 'created'
      ? `${activity.byDisplayName} criou a sala "${activity.roomName}"`
      : `${activity.byDisplayName} começou a assistir${activity.videoTitle ? ` "${activity.videoTitle}"` : ''}`;
    toast(text, 'ok', { label: 'Entrar', href: `#/room/${activity.code}` });

    if (document.hidden && 'Notification' in window && Notification.permission === 'granted') {
      const notification = new Notification('WatchParty', { body: text, icon: '/icon.svg', tag: activity.code });
      notification.onclick = () => { window.focus(); location.hash = `#/room/${activity.code}`; notification.close(); };
    }
  });
  connection.onreconnected(() => connection.invoke('WatchLobby').catch(() => {}));

  try {
    await connection.start();
    await connection.invoke('WatchLobby');
  } catch {
    connection = null; // alerts are a nice-to-have
  }
}

export async function stopLobby() {
  const current = connection;
  connection = null;
  await current?.stop().catch(() => {});
}

export async function enableNotifications() {
  if (!('Notification' in window)) {
    toast('Seu navegador não suporta notificações.', 'error');
    return;
  }
  const permission = await Notification.requestPermission();
  toast(permission === 'granted' ? 'Pronto! Você será avisado quando a galera começar a assistir.' : 'Notificações bloqueadas no navegador.',
    permission === 'granted' ? 'ok' : 'error');
}
