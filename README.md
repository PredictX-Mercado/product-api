# Predizer back

API em .NET 8 para o back-end do Predizer. Inclui autenticacao com ASP.NET Core Identity (cookies e bearer tokens), EF Core com PostgreSQL e Swagger para exploracao dos endpoints.

## Requisitos

- .NET SDK 8.0+
- PostgreSQL 14+ rodando localmente
- Ferramentas do EF Core CLI (`dotnet tool install --global dotnet-ef` se ainda nao tiver)

## Configuracao rapida

1. Crie/ajuste a string de conexao em `src/Product.Api/appsettings.Development.json` (chave `ConnectionStrings:DefaultConnection`) ou defina a variavel de ambiente `ConnectionStrings__DefaultConnection`.
2. Ajuste `Frontend:BaseUrl` e as expiracoes em `IdentityTokens`.
3. Configure SMTP em `EmailSettings` e remetente em `Email`.
4. Opcional: ajuste a lista `Cors:Allow` para os dominios do seu front.

## Restaurar, migrar e rodar

Na raiz do repo:

```bash
dotnet restore

# aplica migracoes do EF Core usando o projeto de dados como origem de migracoes
dotnet ef database update --project src/Product.Data --startup-project src/Product.Api

# executa a API
dotnet run --project src/Product.Api
```

A API sobe, por padrao, nas portas configuradas no `launchSettings.json` (Kestrel e/ou IIS Express).

## Estrutura rápida

- `src/Product.Api` – host da API, configuracao de DI, middlewares, Swagger.
- `src/Product.Business` – regras de negocio, validacoes, servicos.
- `src/Product.Data` – EF Core com PostgreSQL e migracoes; Dapper para consultas pontuais.
- `src/Product.Contracts` / `src/Product.Common` – DTOs, enums, entidades base e utilitarios.

## Docker

Para facilitar o desenvolvimento e execução local, há um `Dockerfile` para a API e um `docker-compose.yml` na raiz.

- Build da imagem:

```bash
docker compose build api
```

- Executar em background (abre a API em `http://localhost:5000`):

```bash
docker compose up -d
```

- Parar e remover containers:

```bash
docker compose down
```

Notas:

- O `docker-compose.yml` mapeia a porta `80` do container para `5000` no host.
- Ajuste variáveis de ambiente (ex.: `ConnectionStrings__DefaultConnection`) via `docker compose run` ou adicionando `environment` no `docker-compose.yml`.

- Visualizar logs em tempo real:

```bash
docker compose logs -f
