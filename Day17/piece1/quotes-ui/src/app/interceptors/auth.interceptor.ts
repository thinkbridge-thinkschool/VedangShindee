import { HttpInterceptorFn } from '@angular/common/http';

const isJwt = (token: string): boolean => token.split('.').length === 3;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('access_token');
  if (!token || !isJwt(token)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
