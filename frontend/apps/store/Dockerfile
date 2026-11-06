# Local-only web image (owner request, dev). Production web = expo export -> CDN (arch §2).
FROM node:24.8.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Expo inlines EXPO_PUBLIC_* at build time.
ARG EXPO_PUBLIC_API_BASE_URL=http://localhost:5081
ENV EXPO_PUBLIC_API_BASE_URL=$EXPO_PUBLIC_API_BASE_URL
RUN npx expo export --platform web

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
