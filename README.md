# 🎬 WatchParty

App para assistir filmes e vídeos **junto com os amigos**, no celular ou no computador.
Você envia um vídeo, cria uma sala e manda o link. O play, o pause e os pulos acontecem para todo mundo ao mesmo tempo, com **chat de texto** e **chat de voz**.
O visual é inspirado no Spotify.

- **Backend:** .NET 10, ASP.NET Core, Clean Architecture + CQRS, EF Core + SQLite, SignalR
- **Frontend:** HTML/CSS/JS puro (sem build), servido pelo próprio backend e instalável como app (PWA)
- **Vídeo:** conversão automática com FFmpeg para MP4/H.264/AAC, que roda em qualquer celular ou navegador
- **Voz:** WebRTC peer-to-peer, com o SignalR fazendo só a "apresentação" entre as pessoas

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
São 21 testes: regras do domínio (posição de reprodução, playlists, códigos de sala) e fluxos completos pelos handlers (cadastro com convite, login, salas, chat, favoritos, permissões) usando SQLite em memória com as migrations reais.

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

Para usar variável de ambiente, troque `:` por `__`, por exemplo `Auth__InviteCode=meu-codigo`.

---

## Próximos passos (ideias)
- Fila de reprodução na sala ("tocar a playlist inteira")
- Legendas `.srt`/`.vtt` (o FFmpeg já consegue extrair as legendas do MKV)
- Reações com emoji por cima do vídeo
- Barra de progresso da conversão
- Armazenamento em nuvem (Cloudflare R2 / S3): é só implementar `IVideoStorage`
- HLS com várias qualidades para quem tem internet mais lenta

> ⚖️ Use com vídeos que você tem direito de compartilhar com seus amigos.
