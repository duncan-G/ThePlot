import { inject, Injectable } from '@angular/core';
import { GreeterClient } from './greet/GreetServiceClientPb';
import { HelloRequest } from './greet/greet_pb';
import { SERVER_URL } from '../server-url.token';
import { createTraceUnaryInterceptor } from '../grpc-trace.interceptor';

@Injectable({ providedIn: 'root' })
export class GreetService {
  private readonly client = new GreeterClient(inject(SERVER_URL) + '/api', null, {
    unaryInterceptors: [createTraceUnaryInterceptor()],
  });

  async sayHello(name: string): Promise<string> {
    const request = new HelloRequest();
    request.setName(name);
    const reply = await this.client.sayHello(request);
    return reply.getMessage();
  }
}
