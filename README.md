# Password Wallet

Projeto pessoal de aprendizado: um gerenciador de senhas para Windows em C# (.NET 8 + WPF).

- Acesso com **pendrive (arquivo-chave) + frase secreta**
- **Chave de recuperação** para o caso de perder o pendrive ou esquecer a frase
- Criptografia com AES-256-GCM, Argon2id e HKDF

## Estrutura planejada

- `Vault.Core`  — biblioteca (criptografia, slots, armazenamento), sem interface
- `Vault.Tests` — testes do Core
- `Vault.App`   — janela WPF
