FROM klusterkite/baseweb:latest
LABEL maintainer="Mikhail Kantarovskiy <kantoramob@gmail.com>"
EXPOSE 3090
WORKDIR /opt/klusterkite/
COPY build/ /opt/klusterkite/
COPY packageCache/ /opt/packageCache/
COPY ["fallBackConfiguration.json", "config.hocon", "./"]

RUN rm /etc/nginx/sites-enabled/default
RUN chmod 777 nuget.exe
CMD /etc/init.d/nginx start && dotnet KlusterKite.NodeManager.Launcher.dll
