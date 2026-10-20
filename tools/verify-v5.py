"""Read-only comparison against SQLite and HTTP. No secrets in the output."""
from pathlib import Path
import sqlite3,json,urllib.request,http.cookiejar,datetime
ROOT=Path(__file__).resolve().parents[1]

def preserve():
    old=sqlite3.connect(ROOT/'artifacts/backups/before-v5-20261020-235955/mellowhouse.db')
    new=sqlite3.connect(ROOT/'src/MellowHouse.Web/App_Data/mellowhouse.db')
    result={'integrity':new.execute('pragma integrity_check').fetchone()[0],'tables':{}}
    for table in ['Orders','Customers','PaymentAttempts','CartLines','Favorites','Products','Categories','DataProtectionKeys','OrderEvents','OrderRefunds']:
        columns=[x[1] for x in old.execute('pragma table_info('+table+')')]
        if table=='Customers':columns.remove('LastActiveUtc') # Normal authenticated page activity.
        names=','.join('"'+c+'"' for c in columns)
        a=old.execute('select '+names+' from '+table).fetchall();b=new.execute('select '+names+' from '+table).fetchall()
        same=sorted(map(repr,a))==sorted(map(repr,b));assert same,table
        result['tables'][table]={'before':len(a),'after':len(b),'unchanged':same}
    result['contactsFilled']=all(json.loads(new.execute("select Json from SiteSettings where Key='site'").fetchone()[0]).get(k) for k in ['phone','email','address','hours'])
    (ROOT/'artifacts/preservation-v5.json').write_text(json.dumps(result,indent=2))
    return result

import sys
if '--preserve-only' in sys.argv:
    print(json.dumps(preserve()));raise SystemExit(0)

jar=http.cookiejar.CookieJar();client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar));base='http://127.0.0.1:5191'
def api(path,body=None,method='GET'):
    headers={'Content-Type':'application/json','X-CSRF-TOKEN':token if 'token' in globals() else ''}
    req=urllib.request.Request(base+path,data=json.dumps(body).encode() if body is not None else None,headers=headers,method=method)
    with client.open(req,timeout=65) as response:return json.load(response)
token=api('/api/state')['csrfToken']
access=json.loads((ROOT/'artifacts/ui-data-v5/admin-access.json').read_text())
api('/api/auth/login',{'email':access['email'],'password':access['password']},'POST');token=api('/api/state')['csrfToken']
c=sqlite3.connect(ROOT/'artifacts/ui-data-v5/mellowhouse.db');c.row_factory=sqlite3.Row
expenses=json.loads(c.execute("select Json from SiteSettings where Key='finance.expenses.v1'").fetchone()[0])
today=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=3)).date()
checks=[]
for days in [1,7,30,90]:
    first=today-datetime.timedelta(days=days-1);start=datetime.datetime.combine(first,datetime.time())-datetime.timedelta(hours=3);end=datetime.datetime.combine(today+datetime.timedelta(days=1),datetime.time())-datetime.timedelta(hours=3)
    paid=list(c.execute('select * from Orders where PaidUtc>=? and PaidUtc<?',(start.isoformat(' '),end.isoformat(' '))))
    refunds=list(c.execute('select * from OrderRefunds where CreatedUtc>=? and CreatedUtc<?',(start.isoformat(' '),end.isoformat(' '))))
    expected={'gross':sum(o['Total'] for o in paid),'refunds':sum(r['Amount'] for r in refunds),'delivery':sum(o['Delivery'] for o in paid)-sum(r['Delivery'] for r in refunds),'productRevenue':sum(o['Subtotal'] for o in paid)-sum(r['Amount']-r['Delivery'] for r in refunds),'operatingExpenses':sum(e['amount'] for e in expenses if not e['archived'] and str(first)<=e['date']<=str(today))}
    cost=0;unknown={}
    operations=[(o,i,line['quantity']) for o in paid for i,line in enumerate(json.loads(o['LinesJson']))]
    for refund in refunds:
        order=c.execute('select * from Orders where Id=?',(refund['OrderId'],)).fetchone()
        operations.extend((order,line['index'],-line['quantity']) for line in json.loads(refund['LinesJson']))
    for order,index,qty in operations:
        costs=json.loads(order['CostsJson'] or '[]');value=costs[index] if index<len(costs) else None
        if value is not None:cost+=qty*value
        else:unknown[(order['Id'],index)]=unknown.get((order['Id'],index),0)+qty
    expected.update(knownCost=cost,collected=expected['gross']-expected['refunds'],missingCost=any(unknown.values()))
    expected['provisionalResult']=expected['collected']-cost-expected['operatingExpenses'];expected['netProfit']=None if expected['missingCost'] else expected['provisionalResult']
    actual=api('/api/admin/report?from='+str(first)+'&to='+str(today))
    for key,value in expected.items():assert actual[key]==value,(days,key,actual[key],value)
    assert actual['collected']==actual['productRevenue']+actual['delivery']
    assert sum(x['total'] for x in actual['expenseCategories'])==actual['operatingExpenses']
    checks.append({'days':days,'from':str(first),'to':str(today),'matched':True,**expected})
(ROOT/'artifacts/finance-v5-reconciliation.json').write_text(json.dumps(checks,indent=2))
print(json.dumps({'preservation':preserve(),'reconciledPeriods':len(checks)}))

# The guided selection/cart path is deterministic. A separate general cafe question exercises NVIDIA.
answer=api('/api/assistant',{'message':'Как у вас забрать заказ?','requestId':'bce63a9b-6d40-4e44-8208-393bf7c68603'},'POST')
settings=api('/api/admin/settings')
probe={'reply':answer['text'],'productIds':answer['productIds'],'provider':settings['assistantProvider'],'lastModel':settings.get('assistantLastModel'),'cartMutation':answer.get('cart') is not None}
(ROOT/'artifacts/assistant-v5-live.json').write_text(json.dumps(probe,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'liveAssistantProvider':probe['provider'],'liveAssistantLastModel':probe['lastModel'],'nonemptyReply':bool(answer['text']),'cartMutation':probe['cartMutation']}))
