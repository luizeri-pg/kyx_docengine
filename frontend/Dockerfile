# Etapa de build: compila o frontend Vite
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
# Produção no Swarm: URL absoluta da API (CORS já permitido no backend). Compose local usa /api + proxy nginx.
ARG VITE_API_URL=/api
ENV VITE_API_URL=${VITE_API_URL}
RUN npm run build

# Runtime: nginx com template envsubst (imagem oficial) — proxy /api/ com DNS dinâmico no Swarm
FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html

RUN rm -f /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./
COPY nginx/templates/default.conf.template /etc/nginx/templates/default.conf.template

# Upstream do serviço DocEngine na mesma rede overlay (nome do serviço no compose/stack).
ENV API_UPSTREAM_HOST=api
ENV API_UPSTREAM_PORT=8080

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
