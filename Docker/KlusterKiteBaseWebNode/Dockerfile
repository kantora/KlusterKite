FROM klusterkite/baseworker:latest
LABEL maintainer="Mikhail Kantarovskiy <kantoramob@gmail.com>"
RUN apt-get update && apt-get install -y nginx && rm -rf /var/lib/apt/lists/*
EXPOSE 80
