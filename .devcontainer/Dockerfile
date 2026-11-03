FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0

# The base image ships an apt source for Yarn.
# That makes every `apt-get update` fail, which breaks the docker-in-docker feature and drops Codespaces
# into recovery mode. Yarn is not used here (the client uses npm), so remove the source before features install.
RUN rm -f /etc/apt/sources.list.d/yarn.list /etc/apt/sources.list.d/yarn*.list \
 && apt-get update \
 && rm -rf /var/lib/apt/lists/*
