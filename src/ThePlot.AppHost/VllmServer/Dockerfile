FROM vllm/vllm-omni:v0.18.0

RUN pip install --no-cache-dir supervisor

COPY supervisord.conf /etc/supervisord.conf

EXPOSE 8001

CMD ["supervisord", "-c", "/etc/supervisord.conf"]
