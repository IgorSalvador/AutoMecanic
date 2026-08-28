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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

# curl é instalado apenas para o HEALTHCHECK. A imagem de runtime não traz
# nenhum cliente HTTP, e o orquestrador precisa de um para sondar a aplicação.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Executa como usuário sem privilégios. A imagem base já define o usuário 'app';
# usá-lo evita que uma falha na aplicação vire root dentro do contêiner.
USER $APP_UID

WORKDIR /aplicacao
COPY --from=publish --chown=$APP_UID:$APP_UID /aplicacao ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

# A verificação usa o endpoint de prontidão, que confirma também o acesso ao banco.
HEALTHCHECK --interval=20s --timeout=5s --start-period=40s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health/pronto || exit 1

ENTRYPOINT ["dotnet", "AutoMecanic.Api.dll"]
