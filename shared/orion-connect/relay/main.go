package main
import (
 "net/http"
 "sync"
 "time"
 "fmt"
 "os"
 "encoding/json"
 "context"
 "strings"
 "io"
)
type hub struct { mu sync.Mutex; clients map[string]map[chan struct{}]bool }
func (h *hub) notify(account string) { h.mu.Lock(); defer h.mu.Unlock(); for c := range h.clients[account] { select {case c<-struct{}{}: default:} } }
func (h *hub) subscribe(account string) (chan struct{},func()) { h.mu.Lock(); defer h.mu.Unlock(); c:=make(chan struct{},1); if h.clients[account]==nil {h.clients[account]=make(map[chan struct{}]bool)}; if len(h.clients[account])>=16 {return nil,func(){}}; h.clients[account][c]=true; return c,func(){h.mu.Lock(); defer h.mu.Unlock(); delete(h.clients[account],c); if len(h.clients[account])==0 {delete(h.clients,account)}} }
func main(){
 // Development-only relay: loopback binding and explicit shared test token.
 // Production requires verified Google identity, per-device sessions and FCM.
 token:=os.Getenv("ORION_TEST_TOKEN"); if len(token)<32 {panic("set a random test token of at least 32 characters")}
 h:=&hub{clients:make(map[string]map[chan struct{}]bool)}
 mux:=http.NewServeMux()
 auth:=func(w http.ResponseWriter,r *http.Request) bool {if r.Header.Get("Authorization")!="Bearer "+token {http.Error(w,"unauthorized",401);return false};return true}
 mux.HandleFunc("POST /notify",func(w http.ResponseWriter,r *http.Request){if !auth(w,r){return}; b,e:=io.ReadAll(http.MaxBytesReader(w,r.Body,128)); if e!=nil || strings.TrimSpace(string(b))!="" {http.Error(w,"empty body required",400);return}; h.notify("test");w.WriteHeader(204)})
 mux.HandleFunc("GET /events",func(w http.ResponseWriter,r *http.Request){if !auth(w,r){return}; f,ok:=w.(http.Flusher);if !ok {http.Error(w,"stream unsupported",500);return}; c,done:=h.subscribe("test");defer done();if c==nil {http.Error(w,"too many streams",429);return};w.Header().Set("Content-Type","text/event-stream");w.Header().Set("Cache-Control","no-store");fmt.Fprint(w,": connected\n\n");f.Flush(); t:=time.NewTicker(20*time.Second);defer t.Stop();ctx,cancel:=context.WithTimeout(r.Context(),10*time.Minute);defer cancel();for{select{case <-ctx.Done():return;case <-c:fmt.Fprint(w,"event: sync\ndata: {}\n\n");case <-t.C:fmt.Fprint(w,": heartbeat\n\n")};f.Flush()}})
 mux.HandleFunc("GET /health",func(w http.ResponseWriter,r *http.Request){json.NewEncoder(w).Encode(map[string]string{"status":"development"})})
 s:=&http.Server{Addr:"127.0.0.1:8787",Handler:mux,ReadHeaderTimeout:5*time.Second,ReadTimeout:10*time.Second,MaxHeaderBytes:8192};panic(s.ListenAndServe())
}
