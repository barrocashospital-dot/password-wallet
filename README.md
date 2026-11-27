# Password Wallet

Gerenciador de senhas pessoal para Windows, feito em C# (.NET 8 + WPF).

## Baixar e usar

Baixe o `PasswordWallet.exe` na página de [**Releases**](https://github.com/barrocashospital-dot/password-wallet/releases/latest) e execute. É um único arquivo, **não precisa instalar nada** (nem o .NET): ele já traz tudo dentro (Windows 10 ou superior, 64 bits).

- O Windows pode mostrar **"O Windows protegeu seu computador"**, porque o programa não tem assinatura digital paga. Clique em **Mais informações** e depois em **Executar assim mesmo**. Se quiser conferir que o arquivo é o publicado, compare o **SHA-256** com o que está nas notas da versão (`Get-FileHash PasswordWallet.exe`).
- Na primeira execução ele extrai algumas bibliotecas para a pasta temporária do Windows (leva um ou dois segundos a mais).
- Seus cofres (`.vault`) e arquivos-chave ficam onde **você** escolher; o programa não grava nada além disso.

## Como funciona

- O cofre é um arquivo `.vault` cifrado com **AES-256-GCM**.
- A chave mestra (DEK) é aleatória e fica guardada em **slots**:
  - **Pendrive + frase:** `Argon2id(frase)` misturado com o `vault.keyfile` do pendrive via HKDF.
  - **Pendrive reserva (opcional):** um segundo pendrive que abre o cofre com a mesma frase.
  - **Recuperação:** uma chave de recuperação de 24 palavras, da lista BIP39 em português (escreva no papel e guarde em lugar seguro). Chaves antigas, de letras e números, continuam valendo.
- Depois de uma recuperação, o app troca a chave mestra e refaz todas as credenciais.
- Na tela **Segurança** dá para trocar a frase (e, se quiser, o arquivo-chave) sem passar pela recuperação: a chave de recuperação continua a mesma.
- Bloqueio automático após 3 minutos sem uso, ao bloquear o Windows (Win+L) e ao suspender o computador. Se o cofre não puder ser salvo nesse momento, uma cópia cifrada vai para `Documentos\PasswordWallet` e a tela de desbloqueio avisa.
- A área de transferência é apagada após 15 s, ao bloquear e ao fechar o programa.
- A gravação é atômica (arquivo temporário + troca de nome) e mantém um `.bak` do estado anterior.

## Telas

Tema escuro, com uma chuva de letras (o alfabeto do Base32) no fundo que fica azul onde o mouse passa, e transições animadas entre as telas.

| Tela | Para quê |
|---|---|
| Início | Criar, abrir ou recuperar um cofre |
| Criar cofre | Frase, arquivo-chave no pendrive e local do cofre |
| Chave de recuperação | 24 palavras numeradas, mostradas uma vez; é preciso digitá-las de volta (bastam as 4 primeiras letras de cada) |
| Desbloquear | Pendrive achado sozinho + frase |
| Recuperar | Chave de recuperação, depois novas credenciais |
| Cofre aberto | Lista com busca (título, usuário ou site), aviso de senhas fracas ou repetidas, detalhes, copiar, gerar senha (escolha entre 16 e 32 caracteres), importar CSV, salvar (Ctrl+S), bloquear (Ctrl+L) |
| Segurança | Trocar a frase ou o arquivo-chave, cadastrar pendrive reserva |

## Estrutura

- `src/Vault.Core`  — criptografia, slots, formato do arquivo, armazenamento (sem interface)
- `tests/Vault.Tests` — testes do Core (195 testes)
- `src/Vault.App`   — janela WPF (MVVM com CommunityToolkit.Mvvm)

## Compilar a partir do código

Requisitos: Windows 10 ou superior e o [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test
dotnet run --project src/Vault.App
```

Para gerar o executável independente (`artifacts\PasswordWallet.exe`):

```powershell
.\publish.ps1
```

## Antes de usar de verdade

Faça o **ensaio de desastre** com um cofre de teste: recupere-o só com o arquivo `.vault` e a chave de recuperação, abra-o sem o pendrive, e confira que o `.bak` abre. Só depois guarde suas senhas reais.

Para trazer senhas de outro lugar, exporte-as em CSV pelo Chrome, Edge, Firefox, Bitwarden, KeePass ou KeePassXC e use **Importar CSV**. Depois de salvar o cofre, **apague o CSV**: ele guarda as senhas sem proteção.

## Limites conhecidos

- `string` do .NET não pode ser apagada da memória com garantia; o app mitiga com bloqueio por inatividade.
- O arquivo-chave só protege como segundo fator se ficar **fora do computador**. O app avisa quando o disco escolhido é fixo ou é o do sistema. Sem pendrive (para testar), o padrão é `Documentos\PasswordWallet`.
- O `.bak` guarda o estado **anterior** e continua abrindo com as credenciais anteriores; o mesmo vale para o `vault.keyfile.old` que uma recuperação deixa no lugar do arquivo-chave trocado. Depois de uma recuperação por roubo do pendrive, apague os dois.
- Trocar a frase pela tela Segurança mantém a mesma chave mestra: quem tiver uma cópia antiga do cofre e as credenciais antigas ainda consegue abrir essa cópia. Se o pendrive foi perdido ou roubado, use **Recuperar cofre**, que troca também a chave mestra.
- O projeto não foi auditado por terceiros. Mantenha **backup** do `.vault`, do pendrive e da chave de recuperação.

## Ideias futuras

Bloqueio ao minimizar, instalador.

## Créditos

A lista de palavras da chave de recuperação vem do BIP-0039 (licença MIT). Veja [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
