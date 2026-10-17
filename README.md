# Password Wallet

Gerenciador de senhas pessoal para Windows, feito em C# (.NET 8 + WPF) como projeto de aprendizado.

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
- `docs/codigo/`    — **cada arquivo explicado, linha a linha** (comece pelo [índice](docs/codigo/README.md))
- `docs/superpowers/` — especificação e plano de aulas

## Executar

```powershell
dotnet test
dotnet run --project src/Vault.App
```

## Antes de usar de verdade

Faça o **ensaio de desastre** com um cofre de teste: recupere-o só com o arquivo `.vault` e a chave de recuperação, abra-o sem o pendrive, e confira que o `.bak` abre. Só depois guarde suas senhas reais.

## Limites conhecidos

- `string` do .NET não pode ser apagada da memória com garantia; o app mitiga com bloqueio por inatividade.
- O arquivo-chave só protege como segundo fator se ficar **fora do computador**. O app avisa quando o disco escolhido é fixo ou é o do sistema.
- O `.bak` guarda o estado **anterior** e continua abrindo com as credenciais anteriores: depois de uma recuperação por roubo do pendrive, apague-o.
- Projeto educacional: não foi auditado. Mantenha **backup** do `.vault`, do pendrive e da chave de recuperação.

## Ideias futuras

Chave de recuperação em 24 palavras, segundo pendrive reserva, importação do KeePass, bloqueio ao minimizar, instalador.
