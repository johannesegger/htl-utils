FROM node:10
WORKDIR /app

# Restore as distinct layers
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

# Copy everything else
COPY server.js ./

RUN ls -la

# Build runtime image
CMD ["node", "server.js"]
