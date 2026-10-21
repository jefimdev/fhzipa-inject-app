/* =============================================================================
   FHZIPA INJECT - site de divulgação
   JavaScript mínimo: menu no celular, amplifying de capturas, copiar o
   resumo SHA-256 e conferência da disponibilidade do instalador.
   Nenhuma biblioteca externa, nenhum rastreamento, nenhum cookie.
   ========================================================================== */
(function () {
  "use strict";

  var dados = {};
  try {
    var bloco = document.getElementById("dados-download");
    if (bloco) dados = JSON.parse(bloco.textContent || "{}");
  } catch (erro) {
    dados = {};
  }

  /* ------------------------------------------------------- formato de bytes */
  function formatarBytes(bytes) {
    if (!bytes && bytes !== 0) return "—";
    var unidades = ["B", "KB", "MB", "GB"];
    var valor = bytes;
    var indice = 0;
    while (valor >= 1024 && indice < unidades.length - 1) {
      valor /= 1024;
      indice += 1;
    }
    var texto = indice === 0 ? String(Math.round(valor)) : valor.toFixed(1).replace(".", ",");
    return texto + " " + unidades[indice];
  }

  function formatarData(iso) {
    if (!iso) return "—";
    var partes = iso.split("-");
    if (partes.length !== 3) return iso;
    var meses = [
      "janeiro", "fevereiro", "março", "abril", "maio", "junho",
      "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    ];
    return Number(partes[2]) + " de " + meses[Number(partes[1]) - 1] + " de " + partes[0];
  }

  /* ---------------------------------------- preenche os dados em toda a pagina */
  function preencher() {
    var mapa = {
      versao: dados.versao,
      plataforma: dados.plataforma,
      arquitetura: dados.arquitetura,
      tamanho: formatarBytes(dados.bytes),
      data: formatarData(dados.data),
      sha256: dados.sha256,
      "nome-arquivo": dados.arquivo
    };
    Object.keys(mapa).forEach(function (chave) {
      var valor = mapa[chave];
      if (!valor) return;
      var alvos = document.querySelectorAll('[data-meta="' + chave + '"]');
      for (var i = 0; i < alvos.length; i++) {
        alvos[i].textContent = valor;
      }
    });

    var semAssinatura = document.querySelectorAll('[data-assinatura]');
    var textoAssinatura = dados.assinado
      ? "Assinado digitalmente"
      : "Não assinado digitalmente";
    for (var j = 0; j < semAssinatura.length; j++) {
      semAssinatura[j].textContent = textoAssinatura;
    }
  }

  /* --------------------------------- confere se o instalador está no lugar */
  function conferirDisponibilidade() {
    var url = dados.url;
    if (!url) return;

    var marcadores = document.querySelectorAll("[data-observacao-download]");
    var textos = document.querySelectorAll("[data-observacao-texto]");

    function definir(estado, mensagem) {
      for (var i = 0; i < marcadores.length; i++) {
        marcadores[i].querySelector(".ponto-status").setAttribute("data-estado", estado);
      }
      for (var j = 0; j < textos.length; j++) {
        textos[j].textContent = mensagem;
      }
    }

    if (typeof fetch !== "function" || !window.fetch) {
      definir("desconhecido", "O instalador é baixado ao clicar no botão.");
      return;
    }

    // aberto direto do disco, sem servidor: não dá para conferir
    if (window.location.protocol === "file:") {
      definir("desconhecido", "O instalador é baixado ao clicar no botão.");
      return;
    }

    fetch(url, { method: "HEAD", cache: "no-store" })
      .then(function (resposta) {
        if (resposta.ok) {
          definir("disponivel", "Instalador disponível para download agora.");
        } else if (resposta.status === 405) {
          definir("disponivel", "Instalador disponível para download agora.");
        } else {
          definir("indisponivel", "O instalador ainda não foi publicado neste endereço.");
        }
      })
      .catch(function () {
        definir("erro", "Não foi possível conferir o instalador agora. Use o botão de download.");
      });
  }

  /* ------------------------------------------------------- menu do celular */
  function montarMenu() {
    var botao = document.querySelector(".botao-menu");
    var navegacao = document.getElementById("navegacao-principal");
    if (!botao || !navegacao) return;

    function alternar(estado) {
      botao.setAttribute("aria-expanded", estado ? "true" : "false");
      navegacao.setAttribute("data-aberta", estado ? "true" : "false");
    }

    botao.addEventListener("click", function () {
      var aberta = botao.getAttribute("aria-expanded") === "true";
      alternar(!aberta);
    });

    navegacao.addEventListener("click", function (evento) {
      if (evento.target.closest("a")) alternar(false);
    });

    document.addEventListener("keydown", function (evento) {
      if (evento.key === "Escape" && botao.getAttribute("aria-expanded") === "true") {
        alternar(false);
        botao.focus();
      }
    });

    window.addEventListener("resize", function () {
      if (window.innerWidth > 860) alternar(false);
    });
  }

  /* ------------------------------------------- ampliar as capturas do app */
  function montarAmpliador() {
    var caixa = document.getElementById("ampliador");
    if (!caixa) return;
    var imagem = document.getElementById("ampliador-imagem");
    var titulo = document.getElementById("ampliador-titulo");
    var focoAnterior = null;

    function abrir(src, legenda) {
      focoAnterior = document.activeElement;
      imagem.src = src;
      imagem.alt = legenda || "";
      titulo.textContent = legenda || "";
      caixa.hidden = false;
      document.body.style.overflow = "hidden";
      var fechar = caixa.querySelector(".ampliador-fechar");
      if (fechar) fechar.focus();
    }

    function fechar() {
      caixa.hidden = true;
      imagem.removeAttribute("src");
      document.body.style.overflow = "";
      if (focoAnterior && focoAnterior.focus) focoAnterior.focus();
    }

    var botoes = document.querySelectorAll("[data-ampliar]");
    for (var i = 0; i < botoes.length; i++) {
      botoes[i].addEventListener("click", function () {
        abrir(this.getAttribute("data-ampliar"), this.getAttribute("data-legenda"));
      });
    }

    var fechadores = caixa.querySelectorAll("[data-ampliador-fechar]");
    for (var j = 0; j < fechadores.length; j++) {
      fechadores[j].addEventListener("click", fechar);
    }

    document.addEventListener("keydown", function (evento) {
      if (caixa.hidden) return;
      if (evento.key === "Escape") {
        fechar();
        return;
      }
      if (evento.key === "Tab") {
        var focaveis = caixa.querySelectorAll("button, [href], img");
        if (!focaveis.length) return;
        var primeiro = focaveis[0];
        var ultimo = focaveis[focaveis.length - 1];
        if (evento.shiftKey && document.activeElement === primeiro) {
          evento.preventDefault();
          ultimo.focus();
        } else if (!evento.shiftKey && document.activeElement === ultimo) {
          evento.preventDefault();
          primeiro.focus();
        }
      }
    });
  }

  /* ----------------------------------------------------- copiar o SHA-256 */
  function montarCopiarHash() {
    var botao = document.querySelector("[data-copiar-hash]");
    if (!botao) return;

    botao.addEventListener("click", function () {
      var valor = dados.sha256 || "";
      if (!valor) return;

      function avisar(texto) {
        botao.textContent = texto;
        window.setTimeout(function () {
          botao.textContent = "Copiar";
        }, 1800);
      }

      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(valor).then(
          function () { avisar("Copiado"); },
          function () { avisar("Não foi possível copiar"); }
        );
        return;
      }

      var campo = document.createElement("textarea");
      campo.value = valor;
      campo.setAttribute("readonly", "");
      campo.style.position = "absolute";
      campo.style.left = "-9999px";
      document.body.appendChild(campo);
      campo.select();
      try {
        document.execCommand("copy");
        avisar("Copiado");
      } catch (erro) {
        avisar("Não foi possível copiar");
      }
      document.body.removeChild(campo);
    });
  }

  preencher();
  montarMenu();
  montarAmpliador();
  montarCopiarHash();
  conferirDisponibilidade();
})();
