# syntax=docker/dockerfile:1.7

# ---------------------------------------------------------------------------
# Estágio 1 — restauração de dependências
#
# Copiar primeiro apenas os arquivos de projeto faz o cache de camadas do Docker
# reaproveitar o restore inteiro enquanto nenhuma dependência muda; alterar código
# não obriga a baixar os pacotes de novo.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS restore
WORKDIR /origem

COPY Directory.Build.props Directory.Packages.props AutoMecanic.slnx ./
COPY src/AutoMecanic.Domain/AutoMecanic.Domain.csproj                 src/AutoMecanic.Domain/
COPY src/AutoMecanic.Application/AutoMecanic.Application.csproj       src/AutoMecanic.Application/
COPY src/AutoMecanic.Infrastructure/AutoMecanic.Infrastructure.csproj src/AutoMecanic.Infrastructure/
COPY src/AutoMecanic.Api/AutoMecanic.Api.csproj                       src/AutoMecanic.Api/
COPY tests/AutoMecanic.UnitTests/AutoMecanic.UnitTests.csproj                 tests/AutoMecanic.UnitTests/
COPY tests/AutoMecanic.IntegrationTests/AutoMecanic.IntegrationTests.csproj   tests/AutoMecanic.IntegrationTests/

RUN dotnet restore src/AutoMecanic.Api/AutoMecanic.Api.csproj

# ---------------------------------------------------------------------------
# Estágio 2 — compilação e publicação
# ---------------------------------------------------------------------------
FROM restore AS publish
WORKDIR /origem

COPY src/ src/

RUN dotnet publish src/AutoMecanic.Api/AutoMecanic.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /aplicacao

# ---------------------------------------------------------------------------
# Estágio 3 — imagem final
#
# Baseada na imagem de runtime (sem o SDK): menos de um terço do tamanho e,
# sobretudo, sem compilador nem ferramentas de build disponíveis a um atacante
# que consiga executar comandos no contêiner.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final

# A imagem "chiseled" traz o runtime .NET e praticamente nada além dele: não há
# shell, gerenciador de pacotes, curl nem os utilitários do Ubuntu que acompanham
# a imagem padrão. A redução da superfície de ataque é medível — ver
# docs/06-relatorio-de-seguranca.md — e as ferramentas que um atacante com
# execução de comandos usaria para se movimentar simplesmente não existem.
#
# A contrapartida: sem shell, o HEALTHCHECK não pode ser um comando qualquer.
# Por isso a própria aplicação implementa o modo sonda "--health-check".
#
# A imagem já roda como usuário sem privilégios (UID 1654); não é preciso USER.

WORKDIR /aplicacao
COPY --from=publish --chown=$APP_UID:$APP_UID /aplicacao ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

# A sonda reexecuta o próprio binário no modo "--health-check", que consulta o
# endpoint de prontidão — e portanto confirma também o acesso ao banco de dados.
HEALTHCHECK --interval=20s --timeout=5s --start-period=40s --retries=5 \
    CMD ["dotnet", "AutoMecanic.Api.dll", "--health-check"]

ENTRYPOINT ["dotnet", "AutoMecanic.Api.dll"]
