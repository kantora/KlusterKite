FROM kylemanna/openvpn:latest
LABEL maintainer="Mikhail Kantarovskiy <kantoramob@gmail.com>"
COPY ./OpenVpn /etc/openvpn
COPY ./OpenVpn /etc/openvpn.orig
CMD cp /etc/openvpn.orig/openvpn.conf /etc/openvpn/ && ovpn_run
