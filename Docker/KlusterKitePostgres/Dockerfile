FROM postgres:17
LABEL maintainer="Mikhail Kantarovskiy <kantoramob@gmail.com>"
COPY updateConf.sh ./docker-entrypoint-initdb.d/
RUN chmod 777 ./docker-entrypoint-initdb.d/updateConf.sh
