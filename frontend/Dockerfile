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

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /origem/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --spider http://localhost/ || exit 1
