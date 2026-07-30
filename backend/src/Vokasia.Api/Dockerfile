FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Vokasia.slnx
RUN dotnet publish src/Vokasia.Api/Vokasia.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# GAP ditemukan+ditambal Gate M0 (DECISIONS.md D23): healthcheck docker-compose.yml pakai
# `wget` — base image aspnet:10.0 (Debian, bukan Alpine) TIDAK menyertakan wget/curl bawaan.
# Tanpa baris ini healthcheck SELALU gagal ("exec: wget: executable file not found"), container
# tetap "unhealthy" selamanya meski app-nya sendiri sehat (log app normal, listening 8080).
# Npgsql mem-probe GSSAPI saat membuka koneksi PostgreSQL; sertakan runtime library Debian
# supaya fallback autentikasi tidak menulis "Cannot load library libgssapi_krb5.so.2" di setiap boot.
RUN apt-get update && apt-get install -y --no-install-recommends wget libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Vokasia.Api.dll"]
