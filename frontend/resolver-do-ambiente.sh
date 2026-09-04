#!/bin/sh
# Escreve o "resolver" do nginx a partir do DNS do próprio contêiner.
#
# Roda antes do nginx subir: a imagem oficial executa os scripts de
# /docker-entrypoint.d na ordem. Sem isto, o proxy_pass com nome resolvido em
# tempo de requisição falharia com "no resolver defined".
set -eu

destino=/etc/nginx/conf.d/00-resolver.conf

# Endereços IPv6 precisam de colchetes na diretiva resolver.
servidores=$(awk '/^nameserver/ { if ($2 ~ /:/) printf "[%s] ", $2; else printf "%s ", $2 }' /etc/resolv.conf)

if [ -z "$servidores" ]; then
    echo "resolver-do-ambiente: nenhum nameserver em /etc/resolv.conf" >&2
    exit 1
fi

# valid=10s: reconsulta em vez de guardar o endereço para sempre, que é o que
# quebra o front quando a API é reimplantada e muda de endereço.
printf 'resolver %svalid=10s;\nresolver_timeout 5s;\n' "$servidores" > "$destino"
