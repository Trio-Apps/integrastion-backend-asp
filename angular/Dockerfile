FROM node:20-alpine AS build

WORKDIR /app
COPY package*.json /app/

RUN npm config set registry https://registry.npmjs.org/ \
 && yarn config set registry https://registry.npmjs.org/ \
 && yarn config set network-timeout 600000 \
 && yarn
COPY . /app

RUN yarn build:prod

FROM nginx:alpine
COPY dynamic-env.json /usr/share/nginx/html
COPY nginx.conf  /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/OrderXChange /usr/share/nginx/html
