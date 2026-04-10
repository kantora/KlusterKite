FROM klusterkite/baseworker:latest
LABEL maintainer="Mikhail Kantarovskiy <kantoramob@gmail.com>"
WORKDIR /opt/klusterkite/
COPY build/ /opt/klusterkite/
COPY packageCache/ /opt/packageCache/
COPY ["config.hocon", "./"]
RUN chmod 777 nuget.exe
CMD bash ./start.sh
