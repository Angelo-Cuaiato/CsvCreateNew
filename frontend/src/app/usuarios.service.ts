import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ListaDeUsuarios, UsuarioDoSistema } from './modelos';

/**
 * Gestão de usuários. Todas as rotas exigem perfil de administrador: a API
 * responde 403 para o resto, e a tela nem oferece o caminho.
 */
@Injectable({ providedIn: 'root' })
export class UsuariosService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/usuarios';

  listar(): Observable<ListaDeUsuarios> {
    return this.http.get<ListaDeUsuarios>(this.base);
  }

  criar(usuario: UsuarioDoSistema & { senha: string }): Observable<UsuarioDoSistema> {
    return this.http.post<UsuarioDoSistema>(this.base, usuario);
  }

  trocarSenha(email: string, senha: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${encodeURIComponent(email)}/senha`, { senha });
  }

  excluir(email: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(email)}`);
  }
}
