FROM python:3.12-slim-bookworm
ENV PYTHONUNBUFFERED=1
RUN apt-get update && apt-get install -y --no-install-recommends git curl ca-certificates libstdc++6 libusb-1.0-0 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY install.sh requirements.txt ./
RUN bash install.sh 127.0.0.1 --root-support && rm -rf .state .deps/root.tar.gz .deps/sdb.zip
COPY server.py tv.py ./
COPY static ./static
RUN useradd --uid 10001 --create-home deck && mkdir -p /app/.state && chown -R deck:deck /app
USER deck
VOLUME /app/.state
CMD ["/app/.venv/bin/python","server.py"]
