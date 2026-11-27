package main
import "testing"
func TestIsolation(t *testing.T){h:=&hub{clients:make(map[string]map[chan struct{}]bool)};a,da:=h.subscribe("a");defer da();b,db:=h.subscribe("b");defer db();h.notify("a");select{case <-a:default:t.Fatal("missing event")};select{case <-b:t.Fatal("cross-account event");default:}}
func TestCoalescing(t *testing.T){h:=&hub{clients:make(map[string]map[chan struct{}]bool)};a,d:=h.subscribe("a");defer d();for i:=0;i<100;i++{h.notify("a")};if len(a)!=1{t.Fatal("unbounded queue")}}
