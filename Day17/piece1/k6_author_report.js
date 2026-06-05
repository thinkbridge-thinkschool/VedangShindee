import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(50)<5000', 'p(99)<20000'],
  },
};

export default function () {
  const res = http.get('http://localhost:5051/api/author-report');
  check(res, { 'status 200': (r) => r.status === 200 });
}
