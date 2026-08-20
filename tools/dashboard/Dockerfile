FROM node:20-alpine

# Docker CLI + compose plugin so this container can drive the HOST daemon
# via the mounted /var/run/docker.sock (Docker-outside-of-Docker).
RUN apk add --no-cache docker-cli docker-cli-compose

WORKDIR /app
COPY server.js /app/server.js

EXPOSE 8080
CMD ["node", "/app/server.js"]
