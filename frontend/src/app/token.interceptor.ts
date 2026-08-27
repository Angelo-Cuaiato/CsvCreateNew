import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { AutenticacaoService } from './autenticacao.service';

/**
 * Põe o token em toda chamada para a API e derruba a sessão quando o backend
 * responde 401 (token expirado, revogado ou inválido).
 */
export const tokenInterceptor: HttpInterceptorFn = (requisicao, seguir) => {
  const autenticacao = inject(AutenticacaoService);
  const token = autenticacao.token();
  const paraLogin = requisicao.url.includes('/api/auth/login');

  const comToken =
    token && !paraLogin
      ? requisicao.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : requisicao;

  return seguir(comToken).pipe(
    catchError((falha: unknown) => {
      if (falha instanceof HttpErrorResponse && falha.status === 401 && !paraLogin) {
        autenticacao.sair();
      }

      return throwError(() => falha);
    }),
  );
};
