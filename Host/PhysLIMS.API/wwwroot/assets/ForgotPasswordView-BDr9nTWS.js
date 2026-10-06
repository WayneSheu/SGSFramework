import{H as c,I as R,J as y,K as F,L as M,M as V,d as A,O as G,P as ce,Q as J,R as de,S as ue,T as u,U as pe,V as fe,W as me,X as ve,Y as K,f as T,Z as k,_ as he,$ as H,a0 as W,a1 as ge,a2 as L,F as j,a3 as D,a4 as xe,a5 as we,o as z,c as P,a as B,a6 as be,g as o,w as a,a7 as ye,i as E,h as s,a8 as ke,B as O,j as C,t as U,G as Q,x as ze,l as $,A as Se,C as Ce,v as N,N as _,D as Pe}from"./index-BOr1uOst.js";import{A as $e,P as Ne,L as Z}from"./AuthLayout-BXVedQAg.js";import{g as _e,N as Ie}from"./Space-BT6tspGD.js";import{N as I}from"./FormItem-n0Zuk4GW.js";import{N as q}from"./Alert-wcPqEbFr.js";const Te=c("steps",`
 width: 100%;
 display: flex;
`,[c("step",`
 position: relative;
 display: flex;
 flex: 1;
 `,[R("disabled","cursor: not-allowed"),R("clickable",`
 cursor: pointer;
 `),y("&:last-child",[c("step-splitor","display: none;")])]),c("step-splitor",`
 background-color: var(--n-splitor-color);
 margin-top: calc(var(--n-step-header-font-size) / 2);
 height: 1px;
 flex: 1;
 align-self: flex-start;
 margin-left: 12px;
 margin-right: 12px;
 transition:
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier);
 `),c("step-content","flex: 1;",[c("step-content-header",`
 color: var(--n-header-text-color);
 margin-top: calc(var(--n-indicator-size) / 2 - var(--n-step-header-font-size) / 2);
 line-height: var(--n-step-header-font-size);
 font-size: var(--n-step-header-font-size);
 position: relative;
 display: flex;
 font-weight: var(--n-step-header-font-weight);
 margin-left: 9px;
 transition:
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier);
 `,[F("title",`
 white-space: nowrap;
 flex: 0;
 `)]),F("description",`
 color: var(--n-description-text-color);
 margin-top: 12px;
 margin-left: 9px;
 transition:
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier);
 `)]),c("step-indicator",`
 background-color: var(--n-indicator-color);
 box-shadow: 0 0 0 1px var(--n-indicator-border-color);
 height: var(--n-indicator-size);
 width: var(--n-indicator-size);
 border-radius: 50%;
 display: flex;
 align-items: center;
 justify-content: center;
 transition:
 background-color .3s var(--n-bezier),
 box-shadow .3s var(--n-bezier);
 `,[c("step-indicator-slot",`
 position: relative;
 width: var(--n-indicator-icon-size);
 height: var(--n-indicator-icon-size);
 font-size: var(--n-indicator-icon-size);
 line-height: var(--n-indicator-icon-size);
 `,[F("index",`
 display: inline-block;
 text-align: center;
 position: absolute;
 left: 0;
 top: 0;
 white-space: nowrap;
 font-size: var(--n-indicator-index-font-size);
 width: var(--n-indicator-icon-size);
 height: var(--n-indicator-icon-size);
 line-height: var(--n-indicator-icon-size);
 color: var(--n-indicator-text-color);
 transition: color .3s var(--n-bezier);
 `,[M()]),c("icon",`
 color: var(--n-indicator-text-color);
 transition: color .3s var(--n-bezier);
 `,[M()]),c("base-icon",`
 color: var(--n-indicator-text-color);
 transition: color .3s var(--n-bezier);
 `,[M()])])]),R("vertical","flex-direction: column;",[V("show-description",[y(">",[c("step","padding-bottom: 8px;")])]),y(">",[c("step","margin-bottom: 16px;",[y("&:last-child","margin-bottom: 0;"),y(">",[c("step-indicator",[y(">",[c("step-splitor",`
 position: absolute;
 bottom: -8px;
 width: 1px;
 margin: 0 !important;
 left: calc(var(--n-indicator-size) / 2);
 height: calc(100% - var(--n-indicator-size));
 `)])]),c("step-content",[F("description","margin-top: 8px;")])])])])]),R("content-bottom",[V("vertical",[y(">",[c("step","flex-direction: column",[y(">",[c("step-line","display: flex;",[y(">",[c("step-splitor",`
 margin-top: 0;
 align-self: center;
 `)])])]),y(">",[c("step-content","margin-top: calc(var(--n-indicator-size) / 2 - var(--n-step-header-font-size) / 2);",[c("step-content-header",`
 margin-left: 0;
 `),c("step-content__description",`
 margin-left: 0;
 `)])])])])])])]);function Be(e,l){return typeof e!="object"||e===null||Array.isArray(e)?null:(e.props||(e.props={}),e.props.internalIndex=l+1,e)}function Re(e){return e.map((l,r)=>Be(l,r))}const Fe=Object.assign(Object.assign({},J.props),{current:Number,status:{type:String,default:"process"},size:{type:String,default:"medium"},vertical:Boolean,contentPlacement:{type:String,default:"right"},"onUpdate:current":[Function,Array],onUpdateCurrent:[Function,Array]}),X=fe("n-steps"),je=A({name:"Steps",props:Fe,slots:Object,setup(e,{slots:l}){const{mergedClsPrefixRef:r,mergedRtlRef:d}=G(e),n=ce("Steps",d,r),p=J("Steps","-steps",Te,de,e,r);return ue(X,{props:e,mergedThemeRef:p,mergedClsPrefixRef:r,stepsSlots:l}),{mergedClsPrefix:r,rtlEnabled:n}},render(){const{mergedClsPrefix:e}=this;return u("div",{class:[`${e}-steps`,this.rtlEnabled&&`${e}-steps--rtl`,this.vertical&&`${e}-steps--vertical`,this.contentPlacement==="bottom"&&`${e}-steps--content-bottom`]},Re(pe(_e(this))))}}),Ee={status:String,title:String,description:String,disabled:Boolean,internalIndex:{type:Number,default:0}},Oe=A({name:"Step",props:Ee,slots:Object,setup(e){const l=me(X,null);l||ve("step","`n-step` must be placed inside `n-steps`.");const{inlineThemeDisabled:r}=G(),{props:d,mergedThemeRef:n,mergedClsPrefixRef:p,stepsSlots:f}=l,m=K(d,"vertical"),b=K(d,"contentPlacement"),g=T(()=>{const{status:i}=e;if(i)return i;{const{internalIndex:t}=e,{current:S}=d;if(S===void 0)return"process";if(t<S)return"finish";if(t===S)return d.status||"process";if(t>S)return"wait"}return"process"}),x=T(()=>{const{value:i}=g,{size:t}=d,{common:{cubicBezierEaseInOut:S},self:{stepHeaderFontWeight:h,[k("stepHeaderFontSize",t)]:Y,[k("indicatorIndexFontSize",t)]:ee,[k("indicatorSize",t)]:te,[k("indicatorIconSize",t)]:ne,[k("indicatorTextColor",i)]:se,[k("indicatorBorderColor",i)]:re,[k("headerTextColor",i)]:oe,[k("splitorColor",i)]:ie,[k("indicatorColor",i)]:ae,[k("descriptionTextColor",i)]:le}}=n.value;return{"--n-bezier":S,"--n-description-text-color":le,"--n-header-text-color":oe,"--n-indicator-border-color":re,"--n-indicator-color":ae,"--n-indicator-icon-size":ne,"--n-indicator-index-font-size":ee,"--n-indicator-size":te,"--n-indicator-text-color":se,"--n-splitor-color":ie,"--n-step-header-font-size":Y,"--n-step-header-font-weight":h}}),v=r?he("step",T(()=>{const{value:i}=g,{size:t}=d;return`${i[0]}${t[0]}`}),x,d):void 0,w=T(()=>{if(e.disabled)return;const{onUpdateCurrent:i,"onUpdate:current":t}=d;return i||t?()=>{i&&H(i,e.internalIndex),t&&H(t,e.internalIndex)}:void 0});return{stepsSlots:f,mergedClsPrefix:p,vertical:m,mergedStatus:g,handleStepClick:w,cssVars:r?void 0:x,themeClass:v==null?void 0:v.themeClass,onRender:v==null?void 0:v.onRender,contentPlacement:b}},render(){const{mergedClsPrefix:e,onRender:l,handleStepClick:r,disabled:d,contentPlacement:n,vertical:p}=this,f=W(this.$slots.default,v=>{const w=v||this.description;return w?u("div",{class:`${e}-step-content__description`},w):null}),m=u("div",{class:`${e}-step-splitor`}),b=u("div",{class:`${e}-step-indicator`,key:n},u("div",{class:`${e}-step-indicator-slot`},u(ge,null,{default:()=>W(this.$slots.icon,v=>{const{mergedStatus:w,stepsSlots:i}=this;return w==="finish"||w==="error"?w==="finish"?u(D,{clsPrefix:e,key:"finish"},{default:()=>L(i["finish-icon"],()=>[u(xe,null)])}):w==="error"?u(D,{clsPrefix:e,key:"error"},{default:()=>L(i["error-icon"],()=>[u(we,null)])}):null:v||u("div",{key:this.internalIndex,class:`${e}-step-indicator-slot__index`},this.internalIndex)})})),p?m:null),g=u("div",{class:`${e}-step-content`},u("div",{class:`${e}-step-content-header`},u("div",{class:`${e}-step-content-header__title`},L(this.$slots.title,()=>[this.title])),!p&&n==="right"?m:null),f);let x;return!p&&n==="bottom"?x=u(j,null,u("div",{class:`${e}-step-line`},b,m),g):x=u(j,null,b,g),l==null||l(),u("div",{class:[`${e}-step`,d&&`${e}-step--disabled`,!d&&r&&`${e}-step--clickable`,this.themeClass,f&&`${e}-step--show-description`,`${e}-step--${this.mergedStatus}-status`],style:this.cssVars,onClick:r},x)}}),Ue={xmlns:"http://www.w3.org/2000/svg","xmlns:xlink":"http://www.w3.org/1999/xlink",viewBox:"0 0 512 512"},Ae=A({name:"KeyOutline",render:function(l,r){return z(),P("svg",Ue,r[0]||(r[0]=[B("path",{d:"M218.1 167.17c0 13 0 25.6 4.1 37.4c-43.1 50.6-156.9 184.3-167.5 194.5a20.17 20.17 0 0 0-6.7 15c0 8.5 5.2 16.7 9.6 21.3c6.6 6.9 34.8 33 40 28c15.4-15 18.5-19 24.8-25.2c9.5-9.3-1-28.3 2.3-36s6.8-9.2 12.5-10.4s15.8 2.9 23.7 3c8.3.1 12.8-3.4 19-9.2c5-4.6 8.6-8.9 8.7-15.6c.2-9-12.8-20.9-3.1-30.4s23.7 6.2 34 5s22.8-15.5 24.1-21.6s-11.7-21.8-9.7-30.7c.7-3 6.8-10 11.4-11s25 6.9 29.6 5.9c5.6-1.2 12.1-7.1 17.4-10.4c15.5 6.7 29.6 9.4 47.7 9.4c68.5 0 124-53.4 124-119.2S408.5 48 340 48s-121.9 53.37-121.9 119.17zM400 144a32 32 0 1 1-32-32a32 32 0 0 1 32 32z",fill:"none",stroke:"currentColor","stroke-linejoin":"round","stroke-width":"32"},null,-1)]))}}),Me={xmlns:"http://www.w3.org/2000/svg","xmlns:xlink":"http://www.w3.org/1999/xlink",viewBox:"0 0 512 512"},Le=A({name:"MailOutline",render:function(l,r){return z(),P("svg",Me,r[0]||(r[0]=[B("rect",{x:"48",y:"96",width:"416",height:"320",rx:"40",ry:"40",fill:"none",stroke:"currentColor","stroke-linecap":"round","stroke-linejoin":"round","stroke-width":"32"},null,-1),B("path",{fill:"none",stroke:"currentColor","stroke-linecap":"round","stroke-linejoin":"round","stroke-width":"32",d:"M112 160l144 112l144-112"},null,-1)]))}}),Ve={class:"stepper-content"},Ke="process",He={__name:"StepperForm",props:{current:{type:Number,default:0},steps:{type:Array,required:!0},prevText:{type:String,default:"上一步"},nextText:{type:String,default:"下一步"},nextLoading:{type:Boolean,default:!1}},emits:["update:current","before-next"],setup(e,{emit:l}){const r=e,d=l,n=m=>m<r.current?"finish":m===r.current?"process":"wait",p=()=>{r.current!==0&&d("update:current",r.current-1)},f=async()=>{if(r.current!==r.steps.length-1)try{await new Promise((m,b)=>{d("before-next",{resolve:m,reject:b})}),d("update:current",r.current+1)}catch{}};return(m,b)=>{var g;return z(),P(j,null,[o(s(je),{current:e.current,status:Ke},{default:a(()=>[(z(!0),P(j,null,ye(e.steps,(x,v)=>(z(),E(s(Oe),{key:x.key,title:x.title,status:x.status??n(v)},null,8,["title","status"]))),128))]),_:1},8,["current"]),B("div",Ve,[ke(m.$slots,(g=e.steps[e.current])==null?void 0:g.key,{},void 0,!0)]),e.current<e.steps.length-1?(z(),E(s(Ie),{key:0,justify:"between",class:"stepper-actions"},{default:a(()=>[o(s(O),{disabled:e.current===0,onClick:p},{default:a(()=>[C(U(e.prevText),1)]),_:1},8,["disabled"]),o(s(O),{type:"primary",loading:e.nextLoading,onClick:f},{default:a(()=>[C(U(e.nextText),1)]),_:1},8,["loading"])]),_:1})):Q("",!0)],64)}}},We=be(He,[["__scopeId","data-v-661b6c7e"]]),De={key:0,class:"login-form"},Ze={key:1,class:"login-form"},qe={class:"login-row login-row--center"},et={__name:"ForgotPasswordView",setup(e){const l=ze(),r=[{key:"account",title:"驗證帳號"},{key:"reset",title:"設定新密碼"}],d=$(0),n=Se({account:"",email:"",code:"",newPassword:"",confirmPassword:""}),p=$(!1),f=$(""),m=$(!1),b=$(""),g=T(()=>n.code.trim()&&n.newPassword&&n.confirmPassword),x=i=>i.length>=8&&/[A-Za-z]/.test(i)&&/\d/.test(i),v=async({resolve:i,reject:t})=>{if(f.value="",!n.account.trim()||!n.email.trim()){f.value="請輸入帳號與 Email",t();return}p.value=!0;try{await l.sendForgotPasswordCode({account:n.account,email:n.email})?i():(f.value=l.error,t())}finally{p.value=!1}},w=async()=>{if(f.value="",!g.value){f.value="請輸入驗證碼與新密碼";return}if(!x(n.newPassword)){f.value="密碼需至少8碼，並包含英文與數字";return}if(n.newPassword!==n.confirmPassword){f.value="兩次輸入的密碼不一致";return}p.value=!0;try{await l.forgotPassword({account:n.account,email:n.email,code:n.code,newPassword:n.newPassword})?(m.value=!0,b.value="密碼重設成功，請使用新密碼登入"):f.value=l.error}finally{p.value=!1}};return(i,t)=>{const S=Ce("router-link");return z(),E($e,{eyebrow:"Password reset",title:"忘記密碼",subtitle:"先驗證帳號與 Email 收取驗證碼，再設定新密碼。"},{default:a(()=>[m.value?(z(),P("div",Ze,[o(s(q),{type:"success","show-icon":!0},{default:a(()=>[C(U(b.value),1)]),_:1}),o(s(O),{class:"login-submit mt-4",type:"primary",size:"large",block:"",onClick:t[6]||(t[6]=h=>i.$router.push("/login"))},{default:a(()=>t[8]||(t[8]=[C(" 前往登入 ")])),_:1})])):(z(),P("div",De,[o(We,{current:d.value,"onUpdate:current":t[5]||(t[5]=h=>d.value=h),steps:r,"next-text":"發送驗證碼","next-loading":p.value,onBeforeNext:v},{account:a(()=>[o(s(I),{label:"帳號","show-feedback":!1},{default:a(()=>[o(s(N),{value:n.account,"onUpdate:value":t[0]||(t[0]=h=>n.account=h),placeholder:"請輸入帳號",size:"large",autocomplete:"username",spellcheck:"false","input-props":{autocapitalize:"off"}},{prefix:a(()=>[o(s(_),null,{default:a(()=>[o(s(Ne))]),_:1})]),_:1},8,["value"])]),_:1}),o(s(I),{label:"Email","show-feedback":!1,class:"mt-3"},{default:a(()=>[o(s(N),{value:n.email,"onUpdate:value":t[1]||(t[1]=h=>n.email=h),placeholder:"請輸入註冊時填寫的 Email",size:"large",autocomplete:"email",spellcheck:"false","input-props":{autocapitalize:"off"}},{prefix:a(()=>[o(s(_),null,{default:a(()=>[o(s(Le))]),_:1})]),_:1},8,["value"])]),_:1})]),reset:a(()=>[o(s(I),{label:"驗證碼","show-feedback":!1},{default:a(()=>[o(s(N),{value:n.code,"onUpdate:value":t[2]||(t[2]=h=>n.code=h),placeholder:"請輸入信箱收到的 6 碼驗證碼",size:"large",autocomplete:"one-time-code"},{prefix:a(()=>[o(s(_),null,{default:a(()=>[o(s(Ae))]),_:1})]),_:1},8,["value"])]),_:1}),o(s(I),{label:"新密碼","show-feedback":!1,class:"mt-3"},{default:a(()=>[o(s(N),{value:n.newPassword,"onUpdate:value":t[3]||(t[3]=h=>n.newPassword=h),type:"password","show-password-on":"click",placeholder:"至少8碼，需包含英文與數字",size:"large",autocomplete:"new-password"},{prefix:a(()=>[o(s(_),null,{default:a(()=>[o(s(Z))]),_:1})]),_:1},8,["value"])]),_:1}),o(s(I),{label:"確認新密碼","show-feedback":!1,class:"mt-3"},{default:a(()=>[o(s(N),{value:n.confirmPassword,"onUpdate:value":t[4]||(t[4]=h=>n.confirmPassword=h),type:"password","show-password-on":"click",placeholder:"請再次輸入新密碼",size:"large",autocomplete:"new-password",onKeyup:Pe(w,["enter"])},{prefix:a(()=>[o(s(_),null,{default:a(()=>[o(s(Z))]),_:1})]),_:1},8,["value"])]),_:1}),o(s(O),{class:"login-submit mt-4",type:"primary",size:"large",block:"",loading:p.value,disabled:p.value||!g.value,onClick:w},{default:a(()=>t[7]||(t[7]=[C(" 確認送出 ")])),_:1},8,["loading","disabled"])]),_:1},8,["current","next-loading"]),f.value?(z(),E(s(q),{key:0,type:"error",class:"mt-3","show-icon":!0},{default:a(()=>[C(U(f.value),1)]),_:1})):Q("",!0)])),B("div",qe,[o(S,{to:"/login",class:"forgot-link"},{default:a(()=>t[9]||(t[9]=[C("返回登入")])),_:1})])]),_:1})}}};export{et as default};
