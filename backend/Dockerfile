# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /origem

# Restaura primeiro só com os .csproj: enquanto as dependências não mudam,
# esta camada fica em cache e o build seguinte é rápido.
COPY FluxoCaixa.sln ./
COPY src/FluxoCaixa.Core/FluxoCaixa.Core.csproj src/FluxoCaixa.Core/
COPY src/FluxoCaixa.Dados/FluxoCaixa.Dados.csproj src/FluxoCaixa.Dados/
COPY src/FluxoCaixa.Api/FluxoCaixa.Api.csproj src/FluxoCaixa.Api/
COPY tests/FluxoCaixa.Core.Tests/FluxoCaixa.Core.Tests.csproj tests/FluxoCaixa.Core.Tests/
COPY tests/FluxoCaixa.Api.Tests/FluxoCaixa.Api.Tests.csproj tests/FluxoCaixa.Api.Tests/
RUN dotnet restore src/FluxoCaixa.Api/FluxoCaixa.Api.csproj

COPY src/ src/
RUN dotnet publish src/FluxoCaixa.Api/FluxoCaixa.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /publicado

# ---------- execução ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /aplicacao

COPY --from=build /publicado ./

# A aplicação não precisa de root para escutar em 8080.
RUN useradd --create-home --shell /usr/sbin/nologin aplicacao
USER aplicacao

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# A sonda é a própria aplicação (dotnet FluxoCaixa.Api.dll --saude): assim a
# imagem não precisa de curl nem de acesso a repositório de pacotes no build.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=5 \
  CMD ["dotnet", "FluxoCaixa.Api.dll", "--saude"]

ENTRYPOINT ["dotnet", "FluxoCaixa.Api.dll"]
