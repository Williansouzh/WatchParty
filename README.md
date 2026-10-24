# 🎬 WatchParty

App para assistir filmes e vídeos **junto com os amigos**, no celular ou no computador.
Você envia um vídeo, cria uma sala e manda o link. O play, o pause e os pulos acontecem para todo mundo ao mesmo tempo, com **chat de texto** e **chat de voz**.
O visual é inspirado no Spotify.

- **Backend:** .NET 10, ASP.NET Core, Clean Architecture + CQRS, EF Core + SQLite, SignalR
- **App (Android/iOS):** Flutter, na pasta [`mobile/`](mobile/)
- **Site:** HTML/CSS/JS puro (sem build), servido pelo próprio backend e instalável como app (PWA)
- O app e o site usam o mesmo servidor e **se misturam na mesma sala**: alguém no celular e alguém no PC assistem juntos
- **Vídeo:** conversão automática com FFmpeg para MP4/H.264/AAC, que roda em qualquer celular ou navegador, mais HLS em várias qualidades
- **Voz:** WebRTC peer-to-peer (padrão) ou, opcionalmente, um servidor [LiveKit](https://livekit.io) para calls maiores
- **Armazenamento:** disco local (padrão) ou, opcionalmente, Cloudflare R2 / Amazon S3

---

## Funcionalidades

| | |
|---|---|
| 🔐 **Contas com convite** | Só entra quem tem o código de convite que você define |
| ⬆️ **Upload de filmes grandes** | O envio vai direto para o disco em streaming, então arquivos de vários GB funcionam. Tem barra de progresso e fila para vários arquivos |
| 🔄 **Conversão automática** | MKV, AVI, MOV etc. são convertidos para tocar em qualquer aparelho, fazendo o mínimo de trabalho possível (só remux, só o áudio ou a conversão completa). Também gera a miniatura |
| 📚 **Biblioteca** | Busca, filtros (Tudo, Curtidos, Meus envios), ❤️ "Vídeos curtidos" e **playlists colaborativas** |
| 📺 **Salas** | Código de 6 letras e link de convite. Dá para trocar o vídeo no meio da sessão |
| ⏯️ **Sincronização** | Play, pause e pulos sincronizados. Quem entra no meio cai no ponto certo, e um ajuste fino de velocidade corrige atrasos pequenos sem pular |
| 💬 **Chat de texto** | Fica salvo por sala e mostra avisos como "Ana pausou em 1:02:10" |
| 🎙️ **Chat de voz** | Entrar ou sair da call, mutar, ver quem está na call e um anel verde em quem está falando |
| 📱 **Mobile** | Layout próprio com navegação inferior e pode ser "instalado" na tela inicial |
| ⏳ **Espera quem está carregando** | Se alguém travar carregando, a sala pausa sozinha e volta a tocar quando todos estiverem prontos |
| 🔤 **Legendas** | Extraídas automaticamente do MKV ou enviadas em `.srt`/`.vtt`; cada pessoa escolhe a sua |
| 📶 **Várias qualidades (HLS)** | 480p/720p/1080p geradas depois da conversão; o player troca sozinho conforme a internet |
| 📋 **Fila e maratona** | Fila de vídeos na sala, "tocar a playlist inteira" e avanço automático no fim do vídeo |
| 👑 **Só o anfitrião controla** | Opção do dono da sala para que só ele dê play, pause e pule |
| 😂 **Reações** | Emojis flutuando por cima do vídeo para todo mundo |
| 📊 **Progresso da conversão** | Porcentagem em tempo real enquanto o FFmpeg trabalha |
| 🔔 **Avisos** | Notificação quando alguém cria uma sala, e aviso quando o vídeo da sala é apagado |
| 🔒 **Segurança** | Limite de upload, cota por pessoa, reserva de espaço em disco, troca de senha e "sair de todos os aparelhos" |
| 💾 **Manutenção** | Backup diário do banco, limpeza de salas paradas e chat antigo, `/health` e logs em JSON |

---

## Como rodar no seu computador

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [FFmpeg](https://ffmpeg.org/download.html) no PATH. No Windows: `winget install ffmpeg`. No Mac: `brew install ffmpeg`. No Linux: `sudo apt install ffmpeg`.
  > Sem o FFmpeg o app funciona, mas os vídeos são publicados do jeito que foram enviados e podem não tocar em alguns aparelhos.

### Passos
```bash
# 1. Defina o código de convite (é o que seus amigos usam para criar conta)
#    em src/WatchParty.Api/appsettings.json -> "Auth": { "InviteCode": "..." }

# 2. Rode
dotnet run --project src/WatchParty.Api
```
Abra **http://localhost:5080**, clique em *Cadastre-se com um convite* e pronto.
O banco (`data/watchparty.db`) e os vídeos (`data/media/`) são criados automaticamente.

### No celular, na mesma rede Wi-Fi
Acesse `http://IP-DO-SEU-PC:5080` (por exemplo `http://192.168.0.10:5080`).
Vídeo, sincronização e chat de texto funcionam assim. **O chat de voz exige HTTPS** (regra dos navegadores para usar o microfone), então use a opção abaixo.

---

## Colocar online para os amigos

### Opção A: túnel do Cloudflare (a mais fácil, grátis e com HTTPS)
Com o app rodando no seu PC:
```bash
cloudflared tunnel --url http://localhost:5080
```
Ele mostra um link `https://algo-aleatorio.trycloudflare.com`. É só mandar para os amigos.
Não precisa abrir porta no roteador. O link muda cada vez que você roda; para ter um endereço fixo, crie um túnel nomeado na sua conta Cloudflare.

### Opção B: Docker (em um servidor/VPS ou no próprio PC)
```bash
# edite Auth__InviteCode no docker-compose.yml
docker compose up -d --build
docker compose logs tunnel   # mostra o link https://....trycloudflare.com
```
Os dados ficam na pasta `./data`. O compose já inclui o túnel do Cloudflare; se você usar outro proxy com HTTPS (Caddy, Nginx), pode remover o serviço `tunnel`.

### Voz entre redes diferentes (TURN)
A voz usa servidores STUN do Google, o que funciona para a maioria das conexões.
Se alguém entrar na call e não ouvir ninguém (redes corporativas, alguns 4G), configure um servidor **TURN** (por exemplo [coturn](https://github.com/coturn/coturn) ou um serviço como Metered/Twilio) em `appsettings.json`:
```json
"WebRtc": {
  "IceServers": [
    { "Urls": [ "stun:stun.l.google.com:19302" ] },
    { "Urls": [ "turn:seu-servidor:3478" ], "Username": "usuario", "Credential": "senha" }
  ]
}
```

### Voz com LiveKit (opcional, para calls maiores)
No modo padrão (malha), cada pessoa manda o áudio para cada outra, o que fica pesado a partir de umas 6 a 8 pessoas. Com um servidor [LiveKit](https://livekit.io), cada um manda o áudio uma vez só para o servidor, que distribui. Funciona também em redes mais restritas.

1. Rode o LiveKit (o binário/Docker oficial, `livekit-server --dev` para testar) ou crie um projeto grátis no **LiveKit Cloud**.
2. Configure o WatchParty:
```json
"Voice": {
  "Provider": "livekit",
  "LiveKit": { "Url": "wss://seu-projeto.livekit.cloud", "ApiKey": "API...", "ApiSecret": "..." }
}
```
O site e o app percebem isso sozinhos (via `/api/config`) e passam a usar o LiveKit. O servidor gera um token por pessoa (`RoomHub.GetVoiceToken`), válido só para a sala em que ela está. Se a configuração estiver incompleta, a voz continua em malha.

### Guardar os vídeos no Cloudflare R2 / Amazon S3 (opcional)
Útil quando o disco do servidor é pequeno. O upload e a conversão continuam acontecendo no servidor; depois disso o MP4, a miniatura, as legendas e o HLS vão para o bucket, e os players baixam direto de lá com URLs assinadas (o vídeo deixa de passar pela banda do servidor).
```json
"Storage": {
  "S3": {
    "Enabled": true,
    "ServiceUrl": "https://<ACCOUNT_ID>.r2.cloudflarestorage.com",
    "Region": "auto",
    "Bucket": "watchparty",
    "AccessKey": "...",
    "SecretKey": "...",
    "UrlExpiryMinutes": 360
  }
}
```
Para Amazon S3, deixe `ServiceUrl` vazio e use a região do bucket. **Libere CORS no bucket** (o HLS do site é baixado via JavaScript):
```json
[{ "AllowedOrigins": ["https://seu-servidor.com"], "AllowedMethods": ["GET", "HEAD"], "AllowedHeaders": ["*"], "MaxAgeSeconds": 3600 }]
```

### Backup e manutenção
- Uma cópia do banco é feita a cada `Backup:IntervalHours` (padrão 24 h) em `data/backups`, mantendo as últimas `Backup:Keep` (7). Para restaurar, pare o servidor e troque `data/watchparty.db` por um dos arquivos.
- Salas sem atividade por `Maintenance:RoomInactiveDays` dias e mensagens de chat mais velhas que `Maintenance:ChatRetentionDays` são apagadas.
- `GET /health` responde com o estado do banco e o espaço livre no disco (bom para Uptime Kuma, Docker etc.). Com `Logging:Json=true` os logs saem em JSON.
- Faça backup também da pasta `data/keys`: sem ela todo mundo precisa logar de novo.

---

## 📱 App Flutter (Android e iOS)

O app fica em `mobile/` e fala com o mesmo servidor do site.

### Gerar o APK do Android
```bash
cd mobile
flutter pub get
flutter build apk --release
# → build/app/outputs/flutter-apk/app-release.apk
```
Mande o `.apk` para os amigos (WhatsApp, Drive…) e instale no celular. O Android vai pedir para permitir a instalação de "fontes desconhecidas".

**Assinatura do APK.** O APK de release é assinado com a chave em `mobile/android/watchparty-release.jks`, configurada em `mobile/android/key.properties`. Esses dois arquivos **não vão para o Git**. Guarde-os num lugar seguro: o Android só aceita atualizar o app se a versão nova tiver a mesma assinatura. Sem `key.properties` o build usa a chave de debug.
> Quem instalou a versão 0.1.0 (assinada com a chave de debug) precisa **desinstalar antes** de instalar a 0.2.0.

Para gerar uma chave nova (só se você ainda não tiver uma):
```bash
keytool -genkey -v -keystore mobile/android/watchparty-release.jks -keyalg RSA -keysize 2048 -validity 10000 -alias watchparty
```
e crie `mobile/android/key.properties`:
```
storePassword=...
keyPassword=...
keyAlias=watchparty
storeFile=watchparty-release.jks
```

**Aviso de atualização.** Copie o APK novo para `data/app/WatchParty.apk` no servidor e ajuste `MobileApp:LatestBuild` (o número depois do `+` em `pubspec.yaml`, por exemplo `2`) e `MobileApp:LatestVersion`. O app mostra um aviso com o link `/download/watchparty.apk`.

**Links de convite.** `watchparty://app/room/CODIGO` abre a sala direto no app. No site, celulares Android veem um botão "Abrir no app".

Ao abrir o app pela primeira vez, informe o **endereço do servidor** (por exemplo `https://algo.trycloudflare.com` ou `http://192.168.0.10:5080` na mesma rede Wi-Fi). Dá para deixar o endereço já embutido no app:
```bash
flutter build apk --release --dart-define=SERVER_URL=https://seu-servidor.com
```

### iPhone
Precisa de um Mac com Xcode: `cd mobile && flutter build ios`. Para instalar no celular dos amigos sem App Store, use o TestFlight, que exige conta de desenvolvedor Apple (US$ 99/ano). Sem conta paga, dá para instalar só no seu próprio iPhone pelo Xcode, e a instalação vale por 7 dias.

### Logo e ícones
Os arquivos da marca ficam em [`branding/`](branding/) (SVG do símbolo, ícone, versão para ícone adaptável e uma cor). Para trocar a logo, edite os SVGs e gere de novo todos os PNGs do Android, iOS e web:
```bash
CHROMIUM_PATH=/caminho/do/chromium node branding/render-icons.mjs branding mobile src/WatchParty.Api/wwwroot
```
O ícone adaptável, o ícone tematizado (Android 13+), o ícone de notificação e a tela de abertura do Android são vetores em `mobile/android/app/src/main/res/drawable/`.

### Como o app é organizado
```
mobile/lib/
├── core/        api.dart (REST com token), hub_client.dart (cliente SignalR próprio),
│                session.dart (servidor + token salvos), models.dart, theme.dart (cores do Spotify)
├── widgets/     cards, capas, diálogos, layout de "playlist" reutilizável
└── features/    auth, home, search, library, playlist, video, upload, rooms,
                 room/ (room_controller.dart = sincronização, voice_chat.dart = WebRTC, player_view.dart)
```
- **Login por token:** o app usa `POST /api/auth/token/login`, e o site continua usando cookie. As duas formas funcionam no mesmo servidor.
- **Cliente SignalR próprio** (`hub_client.dart`, cerca de 150 linhas): o pacote da comunidade não funciona no Flutter Web e trazia dependências desnecessárias. Ele reconecta sozinho, como o cliente oficial.
- **Player com controles próprios:** play/pause, ±10 s, barra de progresso e tela cheia na horizontal. Os controles passam pela sincronização da sala.
- **Voz:** `flutter_webrtc`, no mesmo formato de mensagens do site, então dá para falar do app com quem está no navegador. Com o servidor em modo LiveKit, usa o `livekit_client` (`voice_livekit.dart`).
- **Token guardado com segurança** (`flutter_secure_storage`: Keystore no Android, Keychain no iOS).
- **Picture-in-picture** no Android e tela sempre ligada durante a sala e o upload.

### Testes do app
```bash
cd mobile
flutter analyze
flutter test
# teste contra um servidor rodando:
WATCHPARTY_SERVER=http://localhost:5080 WATCHPARTY_USER=will WATCHPARTY_PASSWORD=senha123 flutter test
```

---

## Arquitetura

```
src/
├── WatchParty.Domain          ← Regras de negócio puras (sem dependências)
│   ├── Users/   User
│   ├── Videos/  Video, Favorite, VideoStatus
│   ├── Playlists/ Playlist (agregado) + PlaylistItem
│   ├── Rooms/   Room (estado de reprodução), ChatMessage, RoomCode
│   └── Common/  Entity, Result, Error
│
├── WatchParty.Application     ← Casos de uso (CQRS)
│   ├── Abstractions/Messaging ← ICommand, IQuery, handlers, ISender (mediator próprio)
│   ├── Abstractions/...       ← Interfaces: IAppDbContext, IVideoStorage, IVideoProcessor, IRoomPresence…
│   ├── Behaviors/             ← Pipeline: Logging → Validation (FluentValidation)
│   └── Features/              ← Um arquivo por caso de uso: Command/Query + Validator + Handler
│       ├── Auth/      RegisterUser, Login, GetUser
│       ├── Videos/    UploadVideo, ProcessVideo, GetLibrary, GetVideo, GetVideoMedia, Update, Delete, ToggleFavorite
│       ├── Playlists/ Create, Update, Delete, Get(s), Add/RemoveVideo
│       ├── Rooms/     CreateRoom, GetRoom(s), ControlPlayback, ChangeRoomVideo, GetPlayback, DeleteRoom
│       └── Chat/      SendChatMessage, GetChatHistory
│
├── WatchParty.Infrastructure  ← Implementações técnicas
│   ├── Persistence/  AppDbContext (SQLite), configurações EF, migrations
│   ├── Storage/      LocalVideoStorage (disco)
│   ├── Media/        FfmpegVideoProcessor + fila (Channel) + BackgroundService
│   ├── Security/     PasswordHasher (PBKDF2)
│   └── Realtime/     InMemoryRoomPresence (quem está em cada sala)
│
└── WatchParty.Api             ← Entrada HTTP / tempo real
    ├── Endpoints/    Minimal APIs (Auth, Videos, Playlists, Rooms)
    ├── Hubs/         RoomHub (SignalR): sync, chat e sinalização de voz
    └── wwwroot/      Frontend estilo Spotify (SPA sem build)

tests/WatchParty.Tests          ← Testes de domínio e de handlers (SQLite em memória)
```

Dependências: `Api → Infrastructure → Application → Domain`. O domínio não conhece ninguém.

**Por que um mediator próprio?** O MediatR passou a ser pago para uso comercial. O `ISender` daqui tem cerca de 50 linhas (`Application/Abstractions/Messaging/Sender.cs`) e oferece o mesmo modelo: requests, handlers e pipeline behaviors.

### Como a sincronização funciona
1. O servidor guarda o estado da sala: *"posição P no instante T (relógio do servidor), tocando ou pausado"*.
2. Quando alguém dá play, pausa ou pula, o navegador chama `RoomHub.Playback`. Isso vira o comando `ControlPlaybackCommand`, que salva o novo estado e o avisa a todos da sala.
3. Cada navegador calcula *onde o vídeo deveria estar agora* = P + (agora − T). Ele estima a diferença entre o seu relógio e o do servidor fazendo algumas medições de ida e volta.
4. A cada 4 segundos cada um confere a diferença: até 0,35 s não faz nada, até 2 s acelera ou desacelera 6% sem pular, e acima disso pula direto para a posição certa.

O vídeo **não passa pelo SignalR**. Cada pessoa baixa direto de `/api/videos/{id}/stream`, com suporte a *range requests*, então dá para pular para qualquer ponto sem baixar o arquivo inteiro.

### Como a voz funciona
Funciona em malha (*mesh*): cada pessoa se conecta diretamente com cada outra via WebRTC, e o servidor só repassa as mensagens iniciais (offer, answer e ICE). Para um grupo de amigos (até umas 6 a 8 pessoas) funciona muito bem e não consome a banda do servidor.
Com `Voice:Provider=livekit`, o áudio passa por um servidor LiveKit; o hub continua controlando quem está na call e quem está mutado, então a interface é a mesma nos dois modos.

### Adicionando um novo caso de uso (exemplo)
```csharp
// Application/Features/Videos/RenameVideo.cs
public sealed record RenameVideoCommand(Guid VideoId, Guid UserId, string Title) : ICommand<Result>;

internal sealed class RenameVideoCommandValidator : AbstractValidator<RenameVideoCommand> { ... }

internal sealed class RenameVideoCommandHandler(IAppDbContext db) : ICommandHandler<RenameVideoCommand, Result>
{
    public async Task<Result> Handle(RenameVideoCommand command, CancellationToken ct) { ... }
}
```
Handlers e validators são registrados automaticamente. No endpoint, basta chamar `sender.Send(new RenameVideoCommand(...))`.

### Banco de dados
Na inicialização, o app aplica as migrations sozinho. Para criar uma nova migration:
```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add NomeDaMudanca -p src/WatchParty.Infrastructure -s src/WatchParty.Api -o Persistence/Migrations
```

---

## Testes
```bash
dotnet test
```
São 45 testes: regras do domínio, fluxos completos pelos handlers (SQLite em memória com as migrations reais), a API e o hub de verdade (`WebApplicationFactory` + cliente SignalR), armazenamento S3 e o token do LiveKit.

### Testes de ponta a ponta (navegador)
Em `e2e/` há testes com Playwright que abrem vários navegadores de verdade: sincronização entre três pessoas, chat, voz, fila, legendas, reações, buffering e o app Flutter (versão web) na mesma sala que o site.
```bash
cd e2e && npm install
# com o servidor rodando em http://localhost:5090 (convite amigos123):
BASE_URL=http://localhost:5090 node run.mjs web-sync web-features
# app + site: gere a versão web do Flutter e monte uma pasta com os dois
(cd ../mobile && flutter build web --release --no-web-resources-cdn) && ./combine-webroot.sh /tmp/webroot
# rode outro servidor com ASPNETCORE_WEBROOT=/tmp/webroot e:
BASE_URL=http://localhost:5100 node run.mjs flutter-web
```
Para usar um Chromium já instalado, defina `CHROMIUM_PATH`.

### CI (GitHub Actions)
`.github/workflows/ci.yml` roda os testes .NET, `flutter analyze`/`flutter test`, gera o APK e a versão web e roda todos os testes E2E. Para o APK do CI sair com a sua assinatura, crie os *secrets* do repositório:
- `ANDROID_KEYSTORE_BASE64`: `base64 -w0 mobile/android/watchparty-release.jks`
- `ANDROID_KEY_PROPERTIES`: o conteúdo do `key.properties`

---

## Configurações (`appsettings.json` ou variáveis de ambiente)

| Chave | Padrão | Para quê |
|---|---|---|
| `Auth:InviteCode` | `troque-este-codigo` | Código exigido no cadastro |
| `ConnectionStrings:Default` | `Data Source=data/watchparty.db` | Banco SQLite |
| `Storage:RootPath` | `data/media` | Onde ficam os vídeos e as miniaturas |
| `Ffmpeg:Preset` / `Ffmpeg:Crf` | `veryfast` / `23` | Velocidade e qualidade da conversão (CRF menor = mais qualidade e arquivo maior) |
| `Ffmpeg:MaxHeight` | `1080` | Reduz vídeos 4K para economizar banda |
| `WebRtc:IceServers` | STUN do Google | Servidores STUN/TURN da voz |
| `Storage:MaxUploadBytes` | 20 GB | Tamanho máximo de um arquivo |
| `Storage:UserQuotaBytes` | `0` (sem limite) | Espaço total que cada pessoa pode usar |
| `Storage:MinFreeBytes` | 2 GB | Recusa uploads que deixariam o disco com menos que isso livre |
| `Storage:S3:*` | desligado | Guardar os vídeos no R2/S3 (veja acima) |
| `Ffmpeg:Hls:Enabled` | `true` | Gerar as versões HLS em várias qualidades |
| `Voice:Provider` | `mesh` | `mesh` ou `livekit` (com `Voice:LiveKit:Url/ApiKey/ApiSecret`) |
| `Backup:Enabled` / `IntervalHours` / `Keep` | `true` / `24` / `7` | Backup automático do banco |
| `Maintenance:RoomInactiveDays` / `ChatRetentionDays` | `30` / `180` | Limpeza automática |
| `MobileApp:LatestBuild` / `LatestVersion` / `ApkPath` | `0` / vazio / `data/app/WatchParty.apk` | Aviso de atualização do app |
| `DataProtection:KeysPath` | `data/keys` | Chaves que mantêm os logins válidos após reiniciar |
| `Logging:Json` | `false` | Logs em JSON |

Para usar variável de ambiente, troque `:` por `__`, por exemplo `Auth__InviteCode=meu-codigo`.

---

## Próximos passos (ideias)
- Testar o app em celulares reais (Android e iPhone) e publicar no TestFlight
- Gravação de "momentos" e marcadores no vídeo
- Moderação pelo anfitrião (expulsar, passar o controle)

> ⚖️ Use com vídeos que você tem direito de compartilhar com seus amigos.
