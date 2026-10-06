import{d as ie,l as W,cr as _t,cs as Bt,T as m,ct as ke,cu as Et,cv as oe,cw as At,V as Lt,W as De,X as jt,f as J,bk as Ne,F as Ve,a3 as Ot,ck as It,bl as Ft,cx as Ht,H as r,I as c,J as $,K as B,M as Dt,O as Nt,Q as Me,cy as Vt,cz as We,U as ce,az as Mt,p as ne,ad as be,z as Ut,S as Kt,Y as I,cA as Xt,bN as qt,Z as F,bi as te,_ as Gt,a0 as _e,cg as fe,$ as ae,c3 as Jt,aJ as Yt,cc as Qt,cB as Zt,ci as ea,o as H,i as N,w as D,c as ta,a7 as ge,h as O,a8 as Be,j as me,t as xe,a6 as aa,k as ra,E as na,a as Ee,N as ue,g as re,G as oa,B as ia,r as sa}from"./index-BOr1uOst.js";import{A as la,a as Ae}from"./StatePanel-Cu9Wk0Pq.js";import{C as Le}from"./CheckmarkCircleOutline-BNBVYBrs.js";import{S as da}from"./SaveOutline-DOXQ64a-.js";const ca=ke(".v-x-scroll",{overflow:"auto",scrollbarWidth:"none"},[ke("&::-webkit-scrollbar",{width:0,height:0})]),ba=ie({name:"XScroll",props:{disabled:Boolean,onScroll:Function},setup(){const e=W(null);function n(b){!(b.currentTarget.offsetWidth<b.currentTarget.scrollWidth)||b.deltaY===0||(b.currentTarget.scrollLeft+=b.deltaY+b.deltaX,b.preventDefault())}const s=_t();return ca.mount({id:"vueuc/x-scroll",head:!0,anchorMetaName:Bt,ssr:s}),Object.assign({selfRef:e,handleWheel:n},{scrollTo(...b){var S;(S=e.value)===null||S===void 0||S.scrollTo(...b)}})},render(){return m("div",{ref:"selfRef",onScroll:this.onScroll,onWheel:this.disabled?void 0:this.handleWheel,class:"v-x-scroll"},this.$slots)}});var fa=/\s/;function ua(e){for(var n=e.length;n--&&fa.test(e.charAt(n)););return n}var pa=/^\s+/;function va(e){return e&&e.slice(0,ua(e)+1).replace(pa,"")}var je=NaN,ha=/^[-+]0x[0-9a-f]+$/i,ga=/^0b[01]+$/i,ma=/^0o[0-7]+$/i,xa=parseInt;function Oe(e){if(typeof e=="number")return e;if(Et(e))return je;if(oe(e)){var n=typeof e.valueOf=="function"?e.valueOf():e;e=oe(n)?n+"":n}if(typeof e!="string")return e===0?e:+e;e=va(e);var s=ga.test(e);return s||ma.test(e)?xa(e.slice(2),s?2:8):ha.test(e)?je:+e}var pe=function(){return At.Date.now()},ya="Expected a function",Sa=Math.max,wa=Math.min;function Ca(e,n,s){var l,b,S,h,x,y,w=0,T=!1,P=!1,E=!0;if(typeof e!="function")throw new TypeError(ya);n=Oe(n)||0,oe(s)&&(T=!!s.leading,P="maxWait"in s,S=P?Sa(Oe(s.maxWait)||0,n):S,E="trailing"in s?!!s.trailing:E);function C(u){var A=l,M=b;return l=b=void 0,w=u,h=e.apply(M,A),h}function R(u){return w=u,x=setTimeout(g,n),T?C(u):h}function k(u){var A=u-y,M=u-w,U=n-A;return P?wa(U,S-M):U}function d(u){var A=u-y,M=u-w;return y===void 0||A>=n||A<0||P&&M>=S}function g(){var u=pe();if(d(u))return o(u);x=setTimeout(g,k(u))}function o(u){return x=void 0,E&&l?C(u):(l=b=void 0,h)}function p(){x!==void 0&&clearTimeout(x),w=0,l=y=b=x=void 0}function _(){return x===void 0?h:o(pe())}function f(){var u=pe(),A=d(u);if(l=arguments,b=this,y=u,A){if(x===void 0)return R(y);if(P)return clearTimeout(x),x=setTimeout(g,n),C(y)}return x===void 0&&(x=setTimeout(g,n)),h}return f.cancel=p,f.flush=_,f}var Ta="Expected a function";function $a(e,n,s){var l=!0,b=!0;if(typeof e!="function")throw new TypeError(Ta);return oe(s)&&(l="leading"in s?!!s.leading:l,b="trailing"in s?!!s.trailing:b),Ca(e,n,{leading:l,maxWait:n,trailing:b})}const Se=Lt("n-tabs"),Ue={tab:[String,Number,Object,Function],name:{type:[String,Number],required:!0},disabled:Boolean,displayDirective:{type:String,default:"if"},closable:{type:Boolean,default:void 0},tabProps:Object,label:[String,Number,Object,Function]},Ra=ie({__TAB_PANE__:!0,name:"TabPane",alias:["TabPanel"],props:Ue,slots:Object,setup(e){const n=De(Se,null);return n||jt("tab-pane","`n-tab-pane` must be placed inside `n-tabs`."),{style:n.paneStyleRef,class:n.paneClassRef,mergedClsPrefix:n.mergedClsPrefixRef}},render(){return m("div",{class:[`${this.mergedClsPrefix}-tab-pane`,this.class],style:this.style},this.$slots)}}),za=Object.assign({internalLeftPadded:Boolean,internalAddable:Boolean,internalCreatedByPane:Boolean},Ht(Ue,["displayDirective"])),ye=ie({__TAB__:!0,inheritAttrs:!1,name:"Tab",props:za,setup(e){const{mergedClsPrefixRef:n,valueRef:s,typeRef:l,closableRef:b,tabStyleRef:S,addTabStyleRef:h,tabClassRef:x,addTabClassRef:y,tabChangeIdRef:w,onBeforeLeaveRef:T,triggerRef:P,handleAdd:E,activateTab:C,handleClose:R}=De(Se);return{trigger:P,mergedClosable:J(()=>{if(e.internalAddable)return!1;const{closable:k}=e;return k===void 0?b.value:k}),style:S,addStyle:h,tabClass:x,addTabClass:y,clsPrefix:n,value:s,type:l,handleClose(k){k.stopPropagation(),!e.disabled&&R(e.name)},activateTab(){if(e.disabled)return;if(e.internalAddable){E();return}const{name:k}=e,d=++w.id;if(k!==s.value){const{value:g}=T;g?Promise.resolve(g(e.name,s.value)).then(o=>{o&&w.id===d&&C(k)}):C(k)}}}},render(){const{internalAddable:e,clsPrefix:n,name:s,disabled:l,label:b,tab:S,value:h,mergedClosable:x,trigger:y,$slots:{default:w}}=this,T=b??S;return m("div",{class:`${n}-tabs-tab-wrapper`},this.internalLeftPadded?m("div",{class:`${n}-tabs-tab-pad`}):null,m("div",Object.assign({key:s,"data-name":s,"data-disabled":l?!0:void 0},Ne({class:[`${n}-tabs-tab`,h===s&&`${n}-tabs-tab--active`,l&&`${n}-tabs-tab--disabled`,x&&`${n}-tabs-tab--closable`,e&&`${n}-tabs-tab--addable`,e?this.addTabClass:this.tabClass],onClick:y==="click"?this.activateTab:void 0,onMouseenter:y==="hover"?this.activateTab:void 0,style:e?this.addStyle:this.style},this.internalCreatedByPane?this.tabProps||{}:this.$attrs)),m("span",{class:`${n}-tabs-tab__label`},e?m(Ve,null,m("div",{class:`${n}-tabs-tab__height-placeholder`}," "),m(Ot,{clsPrefix:n},{default:()=>m(la,null)})):w?w():typeof T=="object"?T:It(T??s)),x&&this.type==="card"?m(Ft,{clsPrefix:n,class:`${n}-tabs-tab__close`,onClick:this.handleClose,disabled:l}):null))}}),Pa=r("tabs",`
 box-sizing: border-box;
 width: 100%;
 display: flex;
 flex-direction: column;
 transition:
 background-color .3s var(--n-bezier),
 border-color .3s var(--n-bezier);
`,[c("segment-type",[r("tabs-rail",[$("&.transition-disabled",[r("tabs-capsule",`
 transition: none;
 `)])])]),c("top",[r("tab-pane",`
 padding: var(--n-pane-padding-top) var(--n-pane-padding-right) var(--n-pane-padding-bottom) var(--n-pane-padding-left);
 `)]),c("left",[r("tab-pane",`
 padding: var(--n-pane-padding-right) var(--n-pane-padding-bottom) var(--n-pane-padding-left) var(--n-pane-padding-top);
 `)]),c("left, right",`
 flex-direction: row;
 `,[r("tabs-bar",`
 width: 2px;
 right: 0;
 transition:
 top .2s var(--n-bezier),
 max-height .2s var(--n-bezier),
 background-color .3s var(--n-bezier);
 `),r("tabs-tab",`
 padding: var(--n-tab-padding-vertical); 
 `)]),c("right",`
 flex-direction: row-reverse;
 `,[r("tab-pane",`
 padding: var(--n-pane-padding-left) var(--n-pane-padding-top) var(--n-pane-padding-right) var(--n-pane-padding-bottom);
 `),r("tabs-bar",`
 left: 0;
 `)]),c("bottom",`
 flex-direction: column-reverse;
 justify-content: flex-end;
 `,[r("tab-pane",`
 padding: var(--n-pane-padding-bottom) var(--n-pane-padding-right) var(--n-pane-padding-top) var(--n-pane-padding-left);
 `),r("tabs-bar",`
 top: 0;
 `)]),r("tabs-rail",`
 position: relative;
 padding: 3px;
 border-radius: var(--n-tab-border-radius);
 width: 100%;
 background-color: var(--n-color-segment);
 transition: background-color .3s var(--n-bezier);
 display: flex;
 align-items: center;
 `,[r("tabs-capsule",`
 border-radius: var(--n-tab-border-radius);
 position: absolute;
 pointer-events: none;
 background-color: var(--n-tab-color-segment);
 box-shadow: 0 1px 3px 0 rgba(0, 0, 0, .08);
 transition: transform 0.3s var(--n-bezier);
 `),r("tabs-tab-wrapper",`
 flex-basis: 0;
 flex-grow: 1;
 display: flex;
 align-items: center;
 justify-content: center;
 `,[r("tabs-tab",`
 overflow: hidden;
 border-radius: var(--n-tab-border-radius);
 width: 100%;
 display: flex;
 align-items: center;
 justify-content: center;
 `,[c("active",`
 font-weight: var(--n-font-weight-strong);
 color: var(--n-tab-text-color-active);
 `),$("&:hover",`
 color: var(--n-tab-text-color-hover);
 `)])])]),c("flex",[r("tabs-nav",`
 width: 100%;
 position: relative;
 `,[r("tabs-wrapper",`
 width: 100%;
 `,[r("tabs-tab",`
 margin-right: 0;
 `)])])]),r("tabs-nav",`
 box-sizing: border-box;
 line-height: 1.5;
 display: flex;
 transition: border-color .3s var(--n-bezier);
 `,[B("prefix, suffix",`
 display: flex;
 align-items: center;
 `),B("prefix","padding-right: 16px;"),B("suffix","padding-left: 16px;")]),c("top, bottom",[$(">",[r("tabs-nav",[r("tabs-nav-scroll-wrapper",[$("&::before",`
 top: 0;
 bottom: 0;
 left: 0;
 width: 20px;
 `),$("&::after",`
 top: 0;
 bottom: 0;
 right: 0;
 width: 20px;
 `),c("shadow-start",[$("&::before",`
 box-shadow: inset 10px 0 8px -8px rgba(0, 0, 0, .12);
 `)]),c("shadow-end",[$("&::after",`
 box-shadow: inset -10px 0 8px -8px rgba(0, 0, 0, .12);
 `)])])])])]),c("left, right",[r("tabs-nav-scroll-content",`
 flex-direction: column;
 `),$(">",[r("tabs-nav",[r("tabs-nav-scroll-wrapper",[$("&::before",`
 top: 0;
 left: 0;
 right: 0;
 height: 20px;
 `),$("&::after",`
 bottom: 0;
 left: 0;
 right: 0;
 height: 20px;
 `),c("shadow-start",[$("&::before",`
 box-shadow: inset 0 10px 8px -8px rgba(0, 0, 0, .12);
 `)]),c("shadow-end",[$("&::after",`
 box-shadow: inset 0 -10px 8px -8px rgba(0, 0, 0, .12);
 `)])])])])]),r("tabs-nav-scroll-wrapper",`
 flex: 1;
 position: relative;
 overflow: hidden;
 `,[r("tabs-nav-y-scroll",`
 height: 100%;
 width: 100%;
 overflow-y: auto; 
 scrollbar-width: none;
 `,[$("&::-webkit-scrollbar, &::-webkit-scrollbar-track-piece, &::-webkit-scrollbar-thumb",`
 width: 0;
 height: 0;
 display: none;
 `)]),$("&::before, &::after",`
 transition: box-shadow .3s var(--n-bezier);
 pointer-events: none;
 content: "";
 position: absolute;
 z-index: 1;
 `)]),r("tabs-nav-scroll-content",`
 display: flex;
 position: relative;
 min-width: 100%;
 min-height: 100%;
 width: fit-content;
 box-sizing: border-box;
 `),r("tabs-wrapper",`
 display: inline-flex;
 flex-wrap: nowrap;
 position: relative;
 `),r("tabs-tab-wrapper",`
 display: flex;
 flex-wrap: nowrap;
 flex-shrink: 0;
 flex-grow: 0;
 `),r("tabs-tab",`
 cursor: pointer;
 white-space: nowrap;
 flex-wrap: nowrap;
 display: inline-flex;
 align-items: center;
 color: var(--n-tab-text-color);
 font-size: var(--n-tab-font-size);
 background-clip: padding-box;
 padding: var(--n-tab-padding);
 transition:
 box-shadow .3s var(--n-bezier),
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier),
 border-color .3s var(--n-bezier);
 `,[c("disabled",{cursor:"not-allowed"}),B("close",`
 margin-left: 6px;
 transition:
 background-color .3s var(--n-bezier),
 color .3s var(--n-bezier);
 `),B("label",`
 display: flex;
 align-items: center;
 z-index: 1;
 `)]),r("tabs-bar",`
 position: absolute;
 bottom: 0;
 height: 2px;
 border-radius: 1px;
 background-color: var(--n-bar-color);
 transition:
 left .2s var(--n-bezier),
 max-width .2s var(--n-bezier),
 opacity .3s var(--n-bezier),
 background-color .3s var(--n-bezier);
 `,[$("&.transition-disabled",`
 transition: none;
 `),c("disabled",`
 background-color: var(--n-tab-text-color-disabled)
 `)]),r("tabs-pane-wrapper",`
 position: relative;
 overflow: hidden;
 transition: max-height .2s var(--n-bezier);
 `),r("tab-pane",`
 color: var(--n-pane-text-color);
 width: 100%;
 transition:
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier),
 opacity .2s var(--n-bezier);
 left: 0;
 right: 0;
 top: 0;
 `,[$("&.next-transition-leave-active, &.prev-transition-leave-active, &.next-transition-enter-active, &.prev-transition-enter-active",`
 transition:
 color .3s var(--n-bezier),
 background-color .3s var(--n-bezier),
 transform .2s var(--n-bezier),
 opacity .2s var(--n-bezier);
 `),$("&.next-transition-leave-active, &.prev-transition-leave-active",`
 position: absolute;
 `),$("&.next-transition-enter-from, &.prev-transition-leave-to",`
 transform: translateX(32px);
 opacity: 0;
 `),$("&.next-transition-leave-to, &.prev-transition-enter-from",`
 transform: translateX(-32px);
 opacity: 0;
 `),$("&.next-transition-leave-from, &.next-transition-enter-to, &.prev-transition-leave-from, &.prev-transition-enter-to",`
 transform: translateX(0);
 opacity: 1;
 `)]),r("tabs-tab-pad",`
 box-sizing: border-box;
 width: var(--n-tab-gap);
 flex-grow: 0;
 flex-shrink: 0;
 `),c("line-type, bar-type",[r("tabs-tab",`
 font-weight: var(--n-tab-font-weight);
 box-sizing: border-box;
 vertical-align: bottom;
 `,[$("&:hover",{color:"var(--n-tab-text-color-hover)"}),c("active",`
 color: var(--n-tab-text-color-active);
 font-weight: var(--n-tab-font-weight-active);
 `),c("disabled",{color:"var(--n-tab-text-color-disabled)"})])]),r("tabs-nav",[c("line-type",[c("top",[B("prefix, suffix",`
 border-bottom: 1px solid var(--n-tab-border-color);
 `),r("tabs-nav-scroll-content",`
 border-bottom: 1px solid var(--n-tab-border-color);
 `),r("tabs-bar",`
 bottom: -1px;
 `)]),c("left",[B("prefix, suffix",`
 border-right: 1px solid var(--n-tab-border-color);
 `),r("tabs-nav-scroll-content",`
 border-right: 1px solid var(--n-tab-border-color);
 `),r("tabs-bar",`
 right: -1px;
 `)]),c("right",[B("prefix, suffix",`
 border-left: 1px solid var(--n-tab-border-color);
 `),r("tabs-nav-scroll-content",`
 border-left: 1px solid var(--n-tab-border-color);
 `),r("tabs-bar",`
 left: -1px;
 `)]),c("bottom",[B("prefix, suffix",`
 border-top: 1px solid var(--n-tab-border-color);
 `),r("tabs-nav-scroll-content",`
 border-top: 1px solid var(--n-tab-border-color);
 `),r("tabs-bar",`
 top: -1px;
 `)]),B("prefix, suffix",`
 transition: border-color .3s var(--n-bezier);
 `),r("tabs-nav-scroll-content",`
 transition: border-color .3s var(--n-bezier);
 `),r("tabs-bar",`
 border-radius: 0;
 `)]),c("card-type",[B("prefix, suffix",`
 transition: border-color .3s var(--n-bezier);
 `),r("tabs-pad",`
 flex-grow: 1;
 transition: border-color .3s var(--n-bezier);
 `),r("tabs-tab-pad",`
 transition: border-color .3s var(--n-bezier);
 `),r("tabs-tab",`
 font-weight: var(--n-tab-font-weight);
 border: 1px solid var(--n-tab-border-color);
 background-color: var(--n-tab-color);
 box-sizing: border-box;
 position: relative;
 vertical-align: bottom;
 display: flex;
 justify-content: space-between;
 font-size: var(--n-tab-font-size);
 color: var(--n-tab-text-color);
 `,[c("addable",`
 padding-left: 8px;
 padding-right: 8px;
 font-size: 16px;
 justify-content: center;
 `,[B("height-placeholder",`
 width: 0;
 font-size: var(--n-tab-font-size);
 `),Dt("disabled",[$("&:hover",`
 color: var(--n-tab-text-color-hover);
 `)])]),c("closable","padding-right: 8px;"),c("active",`
 background-color: #0000;
 font-weight: var(--n-tab-font-weight-active);
 color: var(--n-tab-text-color-active);
 `),c("disabled","color: var(--n-tab-text-color-disabled);")])]),c("left, right",`
 flex-direction: column; 
 `,[B("prefix, suffix",`
 padding: var(--n-tab-padding-vertical);
 `),r("tabs-wrapper",`
 flex-direction: column;
 `),r("tabs-tab-wrapper",`
 flex-direction: column;
 `,[r("tabs-tab-pad",`
 height: var(--n-tab-gap-vertical);
 width: 100%;
 `)])]),c("top",[c("card-type",[r("tabs-scroll-padding","border-bottom: 1px solid var(--n-tab-border-color);"),B("prefix, suffix",`
 border-bottom: 1px solid var(--n-tab-border-color);
 `),r("tabs-tab",`
 border-top-left-radius: var(--n-tab-border-radius);
 border-top-right-radius: var(--n-tab-border-radius);
 `,[c("active",`
 border-bottom: 1px solid #0000;
 `)]),r("tabs-tab-pad",`
 border-bottom: 1px solid var(--n-tab-border-color);
 `),r("tabs-pad",`
 border-bottom: 1px solid var(--n-tab-border-color);
 `)])]),c("left",[c("card-type",[r("tabs-scroll-padding","border-right: 1px solid var(--n-tab-border-color);"),B("prefix, suffix",`
 border-right: 1px solid var(--n-tab-border-color);
 `),r("tabs-tab",`
 border-top-left-radius: var(--n-tab-border-radius);
 border-bottom-left-radius: var(--n-tab-border-radius);
 `,[c("active",`
 border-right: 1px solid #0000;
 `)]),r("tabs-tab-pad",`
 border-right: 1px solid var(--n-tab-border-color);
 `),r("tabs-pad",`
 border-right: 1px solid var(--n-tab-border-color);
 `)])]),c("right",[c("card-type",[r("tabs-scroll-padding","border-left: 1px solid var(--n-tab-border-color);"),B("prefix, suffix",`
 border-left: 1px solid var(--n-tab-border-color);
 `),r("tabs-tab",`
 border-top-right-radius: var(--n-tab-border-radius);
 border-bottom-right-radius: var(--n-tab-border-radius);
 `,[c("active",`
 border-left: 1px solid #0000;
 `)]),r("tabs-tab-pad",`
 border-left: 1px solid var(--n-tab-border-color);
 `),r("tabs-pad",`
 border-left: 1px solid var(--n-tab-border-color);
 `)])]),c("bottom",[c("card-type",[r("tabs-scroll-padding","border-top: 1px solid var(--n-tab-border-color);"),B("prefix, suffix",`
 border-top: 1px solid var(--n-tab-border-color);
 `),r("tabs-tab",`
 border-bottom-left-radius: var(--n-tab-border-radius);
 border-bottom-right-radius: var(--n-tab-border-radius);
 `,[c("active",`
 border-top: 1px solid #0000;
 `)]),r("tabs-tab-pad",`
 border-top: 1px solid var(--n-tab-border-color);
 `),r("tabs-pad",`
 border-top: 1px solid var(--n-tab-border-color);
 `)])])])]),ve=$a,ka=Object.assign(Object.assign({},Me.props),{value:[String,Number],defaultValue:[String,Number],trigger:{type:String,default:"click"},type:{type:String,default:"bar"},closable:Boolean,justifyContent:String,size:String,placement:{type:String,default:"top"},tabStyle:[String,Object],tabClass:String,addTabStyle:[String,Object],addTabClass:String,barWidth:Number,paneClass:String,paneStyle:[String,Object],paneWrapperClass:String,paneWrapperStyle:[String,Object],addable:[Boolean,Object],tabsPadding:{type:Number,default:0},animated:Boolean,onBeforeLeave:Function,onAdd:Function,"onUpdate:value":[Function,Array],onUpdateValue:[Function,Array],onClose:[Function,Array],labelSize:String,activeName:[String,Number],onActiveNameChange:[Function,Array]}),Wa=ie({name:"Tabs",props:ka,slots:Object,setup(e,{slots:n}){var s,l,b,S;const{mergedClsPrefixRef:h,inlineThemeDisabled:x,mergedComponentPropsRef:y}=Nt(e),w=Me("Tabs","-tabs",Pa,Vt,e,h),T=W(null),P=W(null),E=W(null),C=W(null),R=W(null),k=W(null),d=W(!0),g=W(!0),o=We(e,["labelSize","size"]),p=J(()=>{var t,a;if(o.value)return o.value;const i=(a=(t=y==null?void 0:y.value)===null||t===void 0?void 0:t.Tabs)===null||a===void 0?void 0:a.size;return i||"medium"}),_=We(e,["activeName","value"]),f=W((l=(s=_.value)!==null&&s!==void 0?s:e.defaultValue)!==null&&l!==void 0?l:n.default?(S=(b=ce(n.default())[0])===null||b===void 0?void 0:b.props)===null||S===void 0?void 0:S.name:null),u=Mt(_,f),A={id:0},M=J(()=>{if(!(!e.justifyContent||e.type==="card"))return{display:"flex",justifyContent:e.justifyContent}});ne(u,()=>{A.id=0,Q(),Ce()});function U(){var t;const{value:a}=u;return a===null?null:(t=T.value)===null||t===void 0?void 0:t.querySelector(`[data-name="${a}"]`)}function Ke(t){if(e.type==="card")return;const{value:a}=P;if(!a)return;const i=a.style.opacity==="0";if(t){const v=`${h.value}-tabs-bar--disabled`,{barWidth:z,placement:L}=e;if(t.dataset.disabled==="true"?a.classList.add(v):a.classList.remove(v),["top","bottom"].includes(L)){if(we(["top","maxHeight","height"]),typeof z=="number"&&t.offsetWidth>=z){const j=Math.floor((t.offsetWidth-z)/2)+t.offsetLeft;a.style.left=`${j}px`,a.style.maxWidth=`${z}px`}else a.style.left=`${t.offsetLeft}px`,a.style.maxWidth=`${t.offsetWidth}px`;a.style.width="8192px",i&&(a.style.transition="none"),a.offsetWidth,i&&(a.style.transition="",a.style.opacity="1")}else{if(we(["left","maxWidth","width"]),typeof z=="number"&&t.offsetHeight>=z){const j=Math.floor((t.offsetHeight-z)/2)+t.offsetTop;a.style.top=`${j}px`,a.style.maxHeight=`${z}px`}else a.style.top=`${t.offsetTop}px`,a.style.maxHeight=`${t.offsetHeight}px`;a.style.height="8192px",i&&(a.style.transition="none"),a.offsetHeight,i&&(a.style.transition="",a.style.opacity="1")}}}function Xe(){if(e.type==="card")return;const{value:t}=P;t&&(t.style.opacity="0")}function we(t){const{value:a}=P;if(a)for(const i of t)a.style[i]=""}function Q(){if(e.type==="card")return;const t=U();t?Ke(t):Xe()}function Ce(){var t;const a=(t=R.value)===null||t===void 0?void 0:t.$el;if(!a)return;const i=U();if(!i)return;const{scrollLeft:v,offsetWidth:z}=a,{offsetLeft:L,offsetWidth:j}=i;v>L?a.scrollTo({top:0,left:L,behavior:"smooth"}):L+j>v+z&&a.scrollTo({top:0,left:L+j-z,behavior:"smooth"})}const Z=W(null);let se=0,V=null;function qe(t){const a=Z.value;if(a){se=t.getBoundingClientRect().height;const i=`${se}px`,v=()=>{a.style.height=i,a.style.maxHeight=i};V?(v(),V(),V=null):V=v}}function Ge(t){const a=Z.value;if(a){const i=t.getBoundingClientRect().height,v=()=>{document.body.offsetHeight,a.style.maxHeight=`${i}px`,a.style.height=`${Math.max(se,i)}px`};V?(V(),V=null,v()):V=v}}function Je(){const t=Z.value;if(t){t.style.maxHeight="",t.style.height="";const{paneWrapperStyle:a}=e;if(typeof a=="string")t.style.cssText=a;else if(a){const{maxHeight:i,height:v}=a;i!==void 0&&(t.style.maxHeight=i),v!==void 0&&(t.style.height=v)}}}const Te={value:[]},$e=W("next");function Ye(t){const a=u.value;let i="next";for(const v of Te.value){if(v===a)break;if(v===t){i="prev";break}}$e.value=i,Qe(t)}function Qe(t){const{onActiveNameChange:a,onUpdateValue:i,"onUpdate:value":v}=e;a&&ae(a,t),i&&ae(i,t),v&&ae(v,t),f.value=t}function Ze(t){const{onClose:a}=e;a&&ae(a,t)}function Re(){const{value:t}=P;if(!t)return;const a="transition-disabled";t.classList.add(a),Q(),t.classList.remove(a)}const K=W(null);function le({transitionDisabled:t}){const a=T.value;if(!a)return;t&&a.classList.add("transition-disabled");const i=U();i&&K.value&&(K.value.style.width=`${i.offsetWidth}px`,K.value.style.height=`${i.offsetHeight}px`,K.value.style.transform=`translateX(${i.offsetLeft-Jt(getComputedStyle(a).paddingLeft)}px)`,t&&K.value.offsetWidth),t&&a.classList.remove("transition-disabled")}ne([u],()=>{e.type==="segment"&&be(()=>{le({transitionDisabled:!1})})}),Ut(()=>{e.type==="segment"&&le({transitionDisabled:!0})});let ze=0;function et(t){var a;if(t.contentRect.width===0&&t.contentRect.height===0||ze===t.contentRect.width)return;ze=t.contentRect.width;const{type:i}=e;if((i==="line"||i==="bar")&&Re(),i!=="segment"){const{placement:v}=e;de((v==="top"||v==="bottom"?(a=R.value)===null||a===void 0?void 0:a.$el:k.value)||null)}}const tt=ve(et,64);ne([()=>e.justifyContent,()=>e.size],()=>{be(()=>{const{type:t}=e;(t==="line"||t==="bar")&&Re()})});const X=W(!1);function at(t){var a;const{target:i,contentRect:{width:v,height:z}}=t,L=i.parentElement.parentElement.offsetWidth,j=i.parentElement.parentElement.offsetHeight,{placement:G}=e;if(!X.value)G==="top"||G==="bottom"?L<v&&(X.value=!0):j<z&&(X.value=!0);else{const{value:Y}=C;if(!Y)return;G==="top"||G==="bottom"?L-v>Y.$el.offsetWidth&&(X.value=!1):j-z>Y.$el.offsetHeight&&(X.value=!1)}de(((a=R.value)===null||a===void 0?void 0:a.$el)||null)}const rt=ve(at,64);function nt(){const{onAdd:t}=e;t&&t(),be(()=>{const a=U(),{value:i}=R;!a||!i||i.scrollTo({left:a.offsetLeft,top:0,behavior:"smooth"})})}function de(t){if(!t)return;const{placement:a}=e;if(a==="top"||a==="bottom"){const{scrollLeft:i,scrollWidth:v,offsetWidth:z}=t;d.value=i<=0,g.value=i+z>=v}else{const{scrollTop:i,scrollHeight:v,offsetHeight:z}=t;d.value=i<=0,g.value=i+z>=v}}const ot=ve(t=>{de(t.target)},64);Kt(Se,{triggerRef:I(e,"trigger"),tabStyleRef:I(e,"tabStyle"),tabClassRef:I(e,"tabClass"),addTabStyleRef:I(e,"addTabStyle"),addTabClassRef:I(e,"addTabClass"),paneClassRef:I(e,"paneClass"),paneStyleRef:I(e,"paneStyle"),mergedClsPrefixRef:h,typeRef:I(e,"type"),closableRef:I(e,"closable"),valueRef:u,tabChangeIdRef:A,onBeforeLeaveRef:I(e,"onBeforeLeave"),activateTab:Ye,handleClose:Ze,handleAdd:nt}),Xt(()=>{Q(),Ce()}),qt(()=>{const{value:t}=E;if(!t)return;const{value:a}=h,i=`${a}-tabs-nav-scroll-wrapper--shadow-start`,v=`${a}-tabs-nav-scroll-wrapper--shadow-end`;d.value?t.classList.remove(i):t.classList.add(i),g.value?t.classList.remove(v):t.classList.add(v)});const it={syncBarPosition:()=>{Q()}},st=()=>{le({transitionDisabled:!0})},Pe=J(()=>{const{value:t}=p,{type:a}=e,i={card:"Card",bar:"Bar",line:"Line",segment:"Segment"}[a],v=`${t}${i}`,{self:{barColor:z,closeIconColor:L,closeIconColorHover:j,closeIconColorPressed:G,tabColor:Y,tabBorderColor:lt,paneTextColor:dt,tabFontWeight:ct,tabBorderRadius:bt,tabFontWeightActive:ft,colorSegment:ut,fontWeightStrong:pt,tabColorSegment:vt,closeSize:ht,closeIconSize:gt,closeColorHover:mt,closeColorPressed:xt,closeBorderRadius:yt,[F("panePadding",t)]:ee,[F("tabPadding",v)]:St,[F("tabPaddingVertical",v)]:wt,[F("tabGap",v)]:Ct,[F("tabGap",`${v}Vertical`)]:Tt,[F("tabTextColor",a)]:$t,[F("tabTextColorActive",a)]:Rt,[F("tabTextColorHover",a)]:zt,[F("tabTextColorDisabled",a)]:Pt,[F("tabFontSize",t)]:kt},common:{cubicBezierEaseInOut:Wt}}=w.value;return{"--n-bezier":Wt,"--n-color-segment":ut,"--n-bar-color":z,"--n-tab-font-size":kt,"--n-tab-text-color":$t,"--n-tab-text-color-active":Rt,"--n-tab-text-color-disabled":Pt,"--n-tab-text-color-hover":zt,"--n-pane-text-color":dt,"--n-tab-border-color":lt,"--n-tab-border-radius":bt,"--n-close-size":ht,"--n-close-icon-size":gt,"--n-close-color-hover":mt,"--n-close-color-pressed":xt,"--n-close-border-radius":yt,"--n-close-icon-color":L,"--n-close-icon-color-hover":j,"--n-close-icon-color-pressed":G,"--n-tab-color":Y,"--n-tab-font-weight":ct,"--n-tab-font-weight-active":ft,"--n-tab-padding":St,"--n-tab-padding-vertical":wt,"--n-tab-gap":Ct,"--n-tab-gap-vertical":Tt,"--n-pane-padding-left":te(ee,"left"),"--n-pane-padding-right":te(ee,"right"),"--n-pane-padding-top":te(ee,"top"),"--n-pane-padding-bottom":te(ee,"bottom"),"--n-font-weight-strong":pt,"--n-tab-color-segment":vt}}),q=x?Gt("tabs",J(()=>`${p.value[0]}${e.type[0]}`),Pe,e):void 0;return Object.assign({mergedClsPrefix:h,mergedValue:u,renderedNames:new Set,segmentCapsuleElRef:K,tabsPaneWrapperRef:Z,tabsElRef:T,barElRef:P,addTabInstRef:C,xScrollInstRef:R,scrollWrapperElRef:E,addTabFixed:X,tabWrapperStyle:M,handleNavResize:tt,mergedSize:p,handleScroll:ot,handleTabsResize:rt,cssVars:x?void 0:Pe,themeClass:q==null?void 0:q.themeClass,animationDirection:$e,renderNameListRef:Te,yScrollElRef:k,handleSegmentResize:st,onAnimationBeforeLeave:qe,onAnimationEnter:Ge,onAnimationAfterEnter:Je,onRender:q==null?void 0:q.onRender},it)},render(){const{mergedClsPrefix:e,type:n,placement:s,addTabFixed:l,addable:b,mergedSize:S,renderNameListRef:h,onRender:x,paneWrapperClass:y,paneWrapperStyle:w,$slots:{default:T,prefix:P,suffix:E}}=this;x==null||x();const C=T?ce(T()).filter(f=>f.type.__TAB_PANE__===!0):[],R=T?ce(T()).filter(f=>f.type.__TAB__===!0):[],k=!R.length,d=n==="card",g=n==="segment",o=!d&&!g&&this.justifyContent;h.value=[];const p=()=>{const f=m("div",{style:this.tabWrapperStyle,class:`${e}-tabs-wrapper`},o?null:m("div",{class:`${e}-tabs-scroll-padding`,style:s==="top"||s==="bottom"?{width:`${this.tabsPadding}px`}:{height:`${this.tabsPadding}px`}}),k?C.map((u,A)=>(h.value.push(u.props.name),he(m(ye,Object.assign({},u.props,{internalCreatedByPane:!0,internalLeftPadded:A!==0&&(!o||o==="center"||o==="start"||o==="end")}),u.children?{default:u.children.tab}:void 0)))):R.map((u,A)=>(h.value.push(u.props.name),he(A!==0&&!o?He(u):u))),!l&&b&&d?Fe(b,(k?C.length:R.length)!==0):null,o?null:m("div",{class:`${e}-tabs-scroll-padding`,style:{width:`${this.tabsPadding}px`}}));return m("div",{ref:"tabsElRef",class:`${e}-tabs-nav-scroll-content`},d&&b?m(fe,{onResize:this.handleTabsResize},{default:()=>f}):f,d?m("div",{class:`${e}-tabs-pad`}):null,d?null:m("div",{ref:"barElRef",class:`${e}-tabs-bar`}))},_=g?"top":s;return m("div",{class:[`${e}-tabs`,this.themeClass,`${e}-tabs--${n}-type`,`${e}-tabs--${S}-size`,o&&`${e}-tabs--flex`,`${e}-tabs--${_}`],style:this.cssVars},m("div",{class:[`${e}-tabs-nav--${n}-type`,`${e}-tabs-nav--${_}`,`${e}-tabs-nav`]},_e(P,f=>f&&m("div",{class:`${e}-tabs-nav__prefix`},f)),g?m(fe,{onResize:this.handleSegmentResize},{default:()=>m("div",{class:`${e}-tabs-rail`,ref:"tabsElRef"},m("div",{class:`${e}-tabs-capsule`,ref:"segmentCapsuleElRef"},m("div",{class:`${e}-tabs-wrapper`},m("div",{class:`${e}-tabs-tab`}))),k?C.map((f,u)=>(h.value.push(f.props.name),m(ye,Object.assign({},f.props,{internalCreatedByPane:!0,internalLeftPadded:u!==0}),f.children?{default:f.children.tab}:void 0))):R.map((f,u)=>(h.value.push(f.props.name),u===0?f:He(f))))}):m(fe,{onResize:this.handleNavResize},{default:()=>m("div",{class:`${e}-tabs-nav-scroll-wrapper`,ref:"scrollWrapperElRef"},["top","bottom"].includes(_)?m(ba,{ref:"xScrollInstRef",onScroll:this.handleScroll},{default:p}):m("div",{class:`${e}-tabs-nav-y-scroll`,onScroll:this.handleScroll,ref:"yScrollElRef"},p()))}),l&&b&&d?Fe(b,!0):null,_e(E,f=>f&&m("div",{class:`${e}-tabs-nav__suffix`},f))),k&&(this.animated&&(_==="top"||_==="bottom")?m("div",{ref:"tabsPaneWrapperRef",style:w,class:[`${e}-tabs-pane-wrapper`,y]},Ie(C,this.mergedValue,this.renderedNames,this.onAnimationBeforeLeave,this.onAnimationEnter,this.onAnimationAfterEnter,this.animationDirection)):Ie(C,this.mergedValue,this.renderedNames)))}});function Ie(e,n,s,l,b,S,h){const x=[];return e.forEach(y=>{const{name:w,displayDirective:T,"display-directive":P}=y.props,E=R=>T===R||P===R,C=n===w;if(y.key!==void 0&&(y.key=w),C||E("show")||E("show:lazy")&&s.has(w)){s.has(w)||s.add(w);const R=!E("if");x.push(R?Yt(y,[[Qt,C]]):y)}}),h?m(Zt,{name:`${h}-transition`,onBeforeLeave:l,onEnter:b,onAfterEnter:S},{default:()=>x}):x}function Fe(e,n){return m(ye,{ref:"addTabInstRef",key:"__addable",name:"__addable",internalCreatedByPane:!0,internalAddable:!0,internalLeftPadded:n,disabled:typeof e=="object"&&e.disabled})}function He(e){const n=ea(e);return n.props?n.props.internalLeftPadded=!0:n.props={internalLeftPadded:!0},n}function he(e){return Array.isArray(e.dynamicProps)?e.dynamicProps.includes("internalLeftPadded")||e.dynamicProps.push("internalLeftPadded"):e.dynamicProps=["internalLeftPadded"],e}const _a={__name:"TabPanel",props:{active:{type:String,default:""},type:{type:String,default:"card"},panes:{type:Array,required:!0}},emits:["update:active","close"],setup(e){return(n,s)=>(H(),N(O(Wa),{value:e.active,type:e.type,"onUpdate:value":s[0]||(s[0]=l=>n.$emit("update:active",l)),onClose:s[1]||(s[1]=l=>n.$emit("close",l))},{default:D(()=>[(H(!0),ta(Ve,null,ge(e.panes,l=>(H(),N(O(Ra),{key:l.name,name:l.name,closable:l.closable},{tab:D(()=>[Be(n.$slots,`label-${l.name}`,{},()=>[me(xe(l.label),1)])]),default:D(()=>[Be(n.$slots,`pane-${l.name}`)]),_:2},1032,["name","closable"]))),128))]),_:3},8,["value","type"]))}},Ba={class:"batch-edit-tab-label"},Ea={class:"batch-edit-tab-actions"},Aa={__name:"BatchEditPanel",props:{items:{type:Array,default:()=>[]},itemKey:{type:String,required:!0},itemLabel:{type:Function,required:!0},formComponent:{type:Object,required:!0},saveFn:{type:Function,required:!0},formComponentProps:{type:Object,default:()=>({})},closable:{type:Boolean,default:!0}},emits:["saved","excluded"],setup(e,{expose:n,emit:s}){const l=e,b=s,{notify:S}=ra(),h=W([]),x=W(""),y=W({}),w=W(!1),T=J(()=>h.value.map(d=>({name:d.key,closable:l.closable})));ne(()=>l.items,d=>{var _;const g=d.map(f=>String(f[l.itemKey])),o=h.value.map(f=>f.key);g.length===o.length&&g.every(f=>o.includes(f))||(h.value=d.map(f=>({key:String(f[l.itemKey]),item:f,formData:{...f},fieldErrors:{},saved:!1,failed:!1,saving:!1})),x.value=((_=h.value[0])==null?void 0:_.key)??"")},{immediate:!0});const P=(d,g)=>{y.value[d]=g},E=d=>{const g=h.value.findIndex(p=>p.key===d);if(g===-1)return;const[o]=h.value.splice(g,1);if(delete y.value[d],x.value===d){const p=h.value[g]??h.value[g-1];x.value=(p==null?void 0:p.key)??""}b("excluded",o.item)},C=d=>JSON.stringify(d.formData)!==JSON.stringify(d.item),R=async d=>{const g=y.value[d.key];try{await(g==null?void 0:g.validate())}catch{return}d.saving=!0;try{await l.saveFn(d.formData),d.saved=!0,d.failed=!1,d.item={...d.formData}}catch(o){d.failed=!0,S({type:"error",title:"儲存失敗",text:o.message||"發生未知錯誤"})}finally{d.saving=!1}};return n({saveAll:async()=>{w.value=!0;const d=h.value.filter(p=>!p.saved||C(p));let g=0;const o=[];for(const p of d){const _=y.value[p.key];try{await(_==null?void 0:_.validate())}catch{p.failed=!0,o.push({item:p.item,message:"表單驗證未通過"});continue}p.saving=!0;try{await l.saveFn(p.formData),p.saved=!0,p.failed=!1,p.item={...p.formData},g+=1}catch(f){p.failed=!0,o.push({item:p.item,message:f.message||"發生未知錯誤"})}finally{p.saving=!1}}w.value=!1,b("saved",{successCount:g,failCount:o.length,failures:o}),o.length===0?S({type:"success",title:`全部儲存完成，共 ${g} 筆`}):S({type:"warning",title:`${g} 筆成功，${o.length} 筆失敗`,text:o.map(p=>p.message).join("；")})},saving:w}),(d,g)=>(H(),N(_a,{active:x.value,"onUpdate:active":g[0]||(g[0]=o=>x.value=o),panes:T.value,onClose:E},na({_:2},[ge(h.value,o=>({name:`label-${o.key}`,fn:D(()=>[Ee("span",Ba,[me(xe(e.itemLabel(o.item))+" ",1),o.saved?(H(),N(O(ue),{key:0,size:"14",class:"batch-edit-tab-saved"},{default:D(()=>[re(O(Le))]),_:1})):o.failed?(H(),N(O(ue),{key:1,size:"14",class:"batch-edit-tab-failed"},{default:D(()=>[re(O(Ae))]),_:1})):oa("",!0)])])})),ge(h.value,o=>({name:`pane-${o.key}`,fn:D(()=>[Ee("div",Ea,[re(O(ia),{type:o.failed?"error":o.saved&&!C(o)?"success":"primary",dashed:!o.failed&&!(o.saved&&!C(o)),tertiary:o.saved&&!C(o),loading:o.saving,onClick:p=>R(o)},{icon:D(()=>[re(O(ue),null,{default:D(()=>[o.failed?(H(),N(O(Ae),{key:0})):o.saved&&!C(o)?(H(),N(O(Le),{key:1})):(H(),N(O(da),{key:2}))]),_:2},1024)]),default:D(()=>[me(" "+xe(o.failed?"重試":o.saved&&!C(o)?"已儲存":"儲存此筆"),1)]),_:2},1032,["type","dashed","tertiary","loading","onClick"])]),(H(),N(sa(e.formComponent),Ne({ref:p=>P(o.key,p),modelValue:o.formData,"onUpdate:modelValue":p=>o.formData=p,"field-errors":o.fieldErrors},e.formComponentProps),null,16,["modelValue","onUpdate:modelValue","field-errors"]))])}))]),1032,["active","panes"]))}},Fa=aa(Aa,[["__scopeId","data-v-ddd74c0b"]]);export{Fa as B};
