# ---------- build ----------
FROM node:22-alpine AS build
WORKDIR /origem

# Instala as dependências primeiro: enquanto o package-lock não muda, esta
# camada fica em cache.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# ---------- execução ----------
FROM nginx:1.27-alpine AS final

# O modelo é processado na subida; os padrões abaixo reproduzem o que o
# docker compose sempre usou, então nada muda localmente.
COPY default.conf.template /etc/nginx/templates/default.conf.template

ENV PORT=80
ENV API_UPSTREAM=api:8080

# Sem este filtro, o envsubst consideraria todas as variáveis do ambiente e
# poderia apagar as do próprio nginx ($uri, $host e afins).
ENV NGINX_ENVSUBST_FILTER="^(PORT|API_UPSTREAM)$"
COPY --from=build /origem/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --spider "http://localhost:${PORT}/" || exit 1
