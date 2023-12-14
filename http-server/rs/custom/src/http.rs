use std::{net::{TcpStream, TcpListener}, collections::HashMap, io::{BufReader, BufRead, Write, Read}};
use urlencoding::decode;

// TODO: This HTTP implementation uses blocking calls, causing hangs.
//  Browsers keep a connection open to send more requests, so reading from the request can block forever.
//  may_minihttp seems like a better choice for a small, fast build.

pub struct HttpRequest {
    pub verb: String,
    pub path: String,
    pub version: String,
    pub arguments: HashMap<String, String>,
}

impl HttpRequest {
    pub fn new(verb: &str, path: &str, version: &str, arguments: HashMap<String, String>) -> HttpRequest {
        HttpRequest {
            verb: verb.into(),
            path: path.into(),
            version: version.into(),
            arguments,
        }
    }
}

pub struct HttpResponse {
    code: HttpStatus,
    body: String,
}

impl HttpResponse {
    pub fn new(code: HttpStatus, body: String) -> HttpResponse {
        HttpResponse { code, body }
    }

    pub fn ok(body: String) -> HttpResponse {
        HttpResponse::new(HttpStatus::Ok, body)
    }

    pub fn not_found(url: String) -> HttpResponse {
        HttpResponse::new(HttpStatus::NotFound, url)
    }

    pub fn bad_request(message: String) -> HttpResponse {
        HttpResponse::new(HttpStatus::BadRequest, message)
    }
}

pub enum HttpStatus {
    Ok,
    BadRequest,
    NotFound,
    MethodNotAllowed,
    HttpVersionNotSupported,
}

impl HttpStatus {
    pub fn to_string(&self) -> &'static str {
        match self {
            HttpStatus::Ok                      => "200 OK",
            HttpStatus::BadRequest              => "400 Bad Request",
            HttpStatus::NotFound                => "404 Not Found",
            HttpStatus::MethodNotAllowed        => "405 Method Not Allowed",
            HttpStatus::HttpVersionNotSupported => "505 HTTP Version Not Supported",
        }
    }
}

pub type RequestHandler<T> = fn(HttpRequest, &T) -> HttpResponse;

pub fn run<T>(address: &str, port: u16, app_state: &T, handlers: HashMap<String, RequestHandler<T>>) -> Result<(), std::io::Error> {
    let listener = TcpListener::bind(format!("{address}:{port}"))?;
    println!("Listening on {address}:{port}...");

    for stream in listener.incoming() {
        let mut stream = stream?;
        let response = handle_connection(&mut stream, app_state, &handlers);
        send_response(&mut stream, response)?;
    }

    println!("Shutting down.");
    Ok(())
}

pub fn handle_connection<T>(stream: &mut TcpStream, app_state: &T, handlers: &HashMap<String, RequestHandler<T>>) -> HttpResponse {
    if let Ok(request) = parse_request(stream) {
        if let Some(handler) = handlers.get(&request.path) {
            return handler(request, app_state);
        } else {
            return HttpResponse::not_found(request.path.into());
        }
    } else {
        return HttpResponse::bad_request("".into());
    }
}

pub fn health<T>(request: HttpRequest, _: &T) -> HttpResponse {
    HttpResponse::ok(format!("{} {} {}", request.verb, request.path, request.version))
}

// "GET /assess?g=parse,clint,globe,glove HTTP/1.1",
fn parse_request(stream: &mut TcpStream) -> Result<HttpRequest, HttpResponse> {
    let reader = BufReader::new(stream);
    let mut lines = reader.lines();

    if let Some(Ok(line)) = lines.next() {
        print!("{}", line);

        let parts = line.split(' ').collect::<Vec<&str>>();
        if parts.len() < 3 {
            return Err(HttpResponse::bad_request("".into()));
        }

        let verb = parts[0];
        if verb != "GET" {
            return Err(HttpResponse::new(HttpStatus::MethodNotAllowed, verb.into()));
        } 
        
        let version = parts[2];
        if version != "HTTP/1.1" {
            return Err(HttpResponse::new(HttpStatus::HttpVersionNotSupported, version.into()));
        }

        let mut path = parts[1];
        if let Ok(params) = parse_querystring_arguments(&mut path) {
            return Ok(HttpRequest::new(verb, path, version, params));
        } else {
            return Err(HttpResponse::new(HttpStatus::BadRequest, "Unable to parse querystring".into()));
        }
    } else {
        return Err(HttpResponse::bad_request("".into()));
    }
}

fn _show_request(stream: &mut TcpStream) -> Result<String, (String, String)> {
    let buf_reader = BufReader::new(stream);
    let http_request: Vec<_> = buf_reader
        .lines()
        .map(|result| result.unwrap())
        .take_while(|line| !line.is_empty())
        .collect();

    println!("Request: {:#?}", http_request);
    Ok("".into())
}

fn parse_querystring_arguments(request: &mut &str) -> Result<HashMap<String, String>, ()> {
    let mut arguments = HashMap::new();

    let index = request.find('?');
    if let Some(index) = index {
        let (path, query) = request.split_at(index + 1);
        *request = &path[0..path.len()-1];

        for argument in query.split('&') {
            let mut parts = argument.split('=');

            let name = parts.next().ok_or(())?;
            let name = decode(name).map_err(|_| ())?;

            let value = parts.next().ok_or(())?;
            let value = decode(value).map_err(|_| ())?;

            if parts.next().is_some() { return Err(()); }
            arguments.insert(name.into(), value.into());
        }
    }

    Ok(arguments)
}

fn send_response(stream: &mut TcpStream, response: HttpResponse) -> Result<(), std::io::Error> {
    let status = response.code.to_string();
    let body = response.body;
    let length = body.len();
    
    let response = format!("HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {length}\r\n\r\n{body}");
    
    stream.write_all(response.as_bytes())?;
    stream.flush()?;
    
    println!("\t-> {status} {length}");
    Ok(())
}