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
  - **Recuperação:** uma chave de recuperação em Base32 (guarde-a no papel, em lugar seguro).
- Depois de uma recuperação, o app troca a chave mestra e refaz todas as credenciais.
- Bloqueio automático após 3 minutos sem uso; a área de transferência é apagada após 15 s.
- A gravação é atômica (arquivo temporário + troca de nome) e mantém um `.bak` do estado anterior.

## Telas

Tema escuro, com uma chuva de letras (o alfabeto do Base32) no fundo que fica azul onde o mouse passa, e transições animadas entre as telas.

| Tela | Para quê |
|---|---|
| Início | Criar, abrir ou recuperar um cofre |
| Criar cofre | Frase, arquivo-chave no pendrive e local do cofre |
| Chave de recuperação | Mostrada uma vez; é preciso digitá-la de volta |
| Desbloquear | Pendrive achado sozinho + frase |
| Recuperar | Chave de recuperação, depois novas credenciais |
| Cofre aberto | Lista, detalhes, copiar, gerar senha, salvar, bloquear |

## Estrutura

- `src/Vault.Core`  — criptografia, slots, formato do arquivo, armazenamento (sem interface)
- `tests/Vault.Tests` — testes do Core (86 testes)
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

## Limites conhecidos

- `string` do .NET não pode ser apagada da memória com garantia; o app mitiga com bloqueio por inatividade.
- O arquivo-chave só protege como segundo fator se ficar **fora do computador**. O app avisa quando o disco escolhido é fixo ou é o do sistema. Sem pendrive (para testar), o padrão é `Documentos\PasswordWallet`.
- O `.bak` guarda o estado **anterior** e continua abrindo com as credenciais anteriores: depois de uma recuperação por roubo do pendrive, apague-o.
- O projeto não foi auditado por terceiros. Mantenha **backup** do `.vault`, do pendrive e da chave de recuperação.

## Ideias futuras

Chave de recuperação em 24 palavras, segundo pendrive reserva, importação do KeePass, bloqueio ao minimizar, instalador.
